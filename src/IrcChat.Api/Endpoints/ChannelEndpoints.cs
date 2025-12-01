// ============================================================================
// src/IrcChat.Api/Endpoints/ChannelEndpoints.cs
// ============================================================================
// Fichier centralisé pour tous les endpoints concernant les canaux

using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using IrcChat.Api.Authorization;
using IrcChat.Api.Data;
using IrcChat.Api.Hubs;
using IrcChat.Shared.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace IrcChat.Api.Endpoints;

[SuppressMessage("Performance", "CA1862", Justification = "Not translated in SQL requests")]
public static class ChannelEndpoints
{
    public static WebApplication MapChannelEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/channels")
            .WithTags("Channels");

        // GET endpoints
        group.MapGet("", GetChannelsAsync)
            .WithName("GetChannels")
            .WithDescription("Récupère tous les salons avec le nombre d'utilisateurs connectés");

        app.MapGet("/api/my-channels", GetMyChannelsAsync)
            .WithName("GetMyChannels")
            .WithDescription("Récupère les salons auxquels l'utilisateur est connecté");

        group.MapGet("/{channel}/users", GetConnectedUsersAsync)
            .WithName("GetConnectedUsers")
            .WithDescription("Récupère les utilisateurs connectés à un salon");

        // POST endpoints
        group.MapPost("", CreateChannelAsync)
            .RequireAuthorization(AuthorizationPolicies.IsReserved)
            .WithName("CreateChannel")
            .WithDescription("Crée un nouveau salon (réservé aux utilisateurs réservés)");

        // PUT endpoints
        group.MapPut("/{channelName}", UpdateChannelAsync)
            .RequireAuthorization(AuthorizationPolicies.CanModifyChannel)
            .WithName("UpdateChannel")
            .WithDescription("Modifie la description d'un salon (créateur ou admin uniquement)");

        // DELETE endpoints
        group.MapDelete("/{channelName}", DeleteChannelAsync)
            .RequireAuthorization(AuthorizationPolicies.CanModifyChannel)
            .WithName("DeleteChannel")
            .WithDescription("Supprime un salon (créateur ou admin uniquement)");

        // POST toggle mute endpoint
        group.MapPost("/{channelName}/toggle-mute", ToggleMuteAsync)
            .RequireAuthorization(AuthorizationPolicies.CanModifyChannel)
            .WithName("ToggleChannelMute")
            .WithDescription("Active/désactive le mode muet pour un salon (créateur ou admin uniquement)");

        return app;
    }

    // ========================================================================
    // GET ENDPOINTS
    // ========================================================================

    private static async Task<IResult> GetChannelsAsync(ChatDbContext db, ILogger<Program> logger)
    {
        try
        {
            var query = db.Channels
                .GroupJoin(
                    db.ConnectedUsers,
                    c => c.Name,
                    u => u.Channel,
                    (channel, users) => new ChannelInfo
                    {
                        Channel = channel,
                        ConnectedUsersCount = users
                            .Where(u => !string.IsNullOrEmpty(u.Channel))
                            .Select(u => u.Username)
                            .Distinct()
                            .Count()
                    });
            var channels = await GetChannelListAsync(query);

            logger.LogInformation("Récupération de tous les salons: {ChannelCount} salons trouvés", channels.Count);
            return Results.Ok(channels);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors de la récupération des salons");
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> GetMyChannelsAsync(
        string? username,
        ChatDbContext db,
        ILogger<Program> logger)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            logger.LogWarning("Tentative d'accès à GetMyChannels sans username");
            return Results.BadRequest(new { error = "username_required", message = "Le nom d'utilisateur est requis" });
        }

        try
        {
            var query = db.ConnectedUsers
                .Where(u => u.Username == username && !string.IsNullOrEmpty(u.Channel))
                .Select(u => u.Channel!)
                .Distinct()
                .Join(
                    db.Channels,
                    channelName => channelName,
                    channel => channel.Name,
                    (channelName, channel) => new ChannelInfo
                    {
                        Channel = channel,
                        ConnectedUsersCount = db.ConnectedUsers
                            .Where(u => u.Channel == channel.Name)
                            .Select(u => u.Username)
                            .Distinct()
                            .Count()
                    });
            var channels = await GetChannelListAsync(query);

            logger.LogInformation("Récupération des salons pour l'utilisateur {Username}: {ChannelCount} salons", username, channels.Count);
            return Results.Ok(channels);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors de la récupération des salons pour {Username}", username);
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }
    }
    private static async Task<IResult> GetConnectedUsersAsync(
        string channel,
        ChatDbContext db,
        ILogger<Program> logger)
    {
        try
        {
            var users = await db.ConnectedUsers
                .Where(u => u.Channel == channel)
                .OrderBy(u => u.Username)
                .Select(u => new User
                {
                    UserId = u.UserId,
                    Username = u.Username,
                    ConnectedAt = u.ConnectedAt,
                })
                .Distinct()
                .ToListAsync();

            logger.LogInformation("Récupération des utilisateurs du salon {Channel}: {UserCount} utilisateurs", channel, users.Count);
            return Results.Ok(users);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors de la récupération des utilisateurs du salon {Channel}", channel);
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    // ========================================================================
    // POST ENDPOINTS
    // ========================================================================

    private static async Task<IResult> CreateChannelAsync(
        Channel request,
        ChatDbContext db,
        HttpContext context,
        IHubContext<ChatHub> hubContext,
        ILogger<Program> logger)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            logger.LogWarning("Tentative de création de salon avec un nom vide");
            return Results.BadRequest(new { error = "missing_channel_name", message = "Le nom du canal est requis" });
        }

        var username = context.User.Claims
            .FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value ?? string.Empty;

        var channelExists = await db.Channels
            .AnyAsync(c => c.Name.ToLower() == request.Name.ToLower());

        if (channelExists)
        {
            logger.LogWarning("Tentative de création d'un salon déjà existant: {ChannelName}", request.Name);
            return Results.BadRequest(new { error = "channel_exists", message = "Ce canal existe déjà" });
        }

        var channel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            CreatedBy = username,
            CreatedAt = DateTime.UtcNow,
            ActiveManager = username,
            IsMuted = false,
            ConnectedUsersCount = 0
        };

        db.Channels.Add(channel);
        await db.SaveChangesAsync();

        await hubContext.Clients.All
            .SendAsync("ChannelListUpdated");

        logger.LogInformation("Nouveau salon créé: {ChannelName} par {Username} avec description: {Description}",
            channel.Name, request.CreatedBy, request.Description ?? "aucune");

        return Results.Created($"/api/channels/{channel.Id}", channel);
    }

    // ========================================================================
    // PUT ENDPOINTS
    // ========================================================================

    private static async Task<IResult> UpdateChannelAsync(
        string channelName,
        Channel request,
        ChatDbContext db,
        HttpContext context,
        ILogger<Program> logger)
    {
        var channel = await db.Channels
            .FirstOrDefaultAsync(c => c.Name.ToLower() == channelName.ToLower());

        if (channel == null)
        {
            logger.LogWarning("Tentative de modification d'un salon inexistant: {ChannelName}", channelName);
            return Results.NotFound(new { error = "channel_not_found", message = "Ce canal n'existe pas" });
        }

        var username = context.User.Claims
            .FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value ?? string.Empty;

        var originalDescription = channel.Description;

        if (request.Description != null)
        {
            channel.Description = request.Description.Trim();
        }

        await db.SaveChangesAsync();

        logger.LogInformation(
            "Salon {ChannelName} modifié par {Username}. Description: {OldDescription} -> {NewDescription}",
            channelName, username, originalDescription ?? "(vide)", channel.Description ?? "(vide)");

        return Results.Ok(new
        {
            id = channel.Id,
            name = channel.Name,
            description = channel.Description,
            createdBy = channel.CreatedBy,
            createdAt = channel.CreatedAt,
            isMuted = channel.IsMuted,
            activeManager = channel.ActiveManager,
            message = "Le canal a été modifié avec succès"
        });
    }

    // ========================================================================
    // DELETE ENDPOINTS
    // ========================================================================

    private static async Task<IResult> DeleteChannelAsync(
        string channelName,
        ChatDbContext db,
        HttpContext context,
        IHubContext<ChatHub> hubContext,
        ILogger<Program> logger)
    {
        var channel = await db.Channels
            .FirstOrDefaultAsync(c => c.Name.ToLower() == channelName.ToLower());

        if (channel == null)
        {
            logger.LogWarning("Tentative de suppression d'un salon inexistant: {ChannelName}", channelName);
            return Results.NotFound(new { error = "channel_not_found" });
        }

        var connectedUsers = await db.ConnectedUsers
            .Where(u => u.Channel!.ToLower() == channelName.ToLower())
            .ToListAsync();

        db.ConnectedUsers.RemoveRange(connectedUsers);

        var messages = await db.Messages
            .Where(m => m.Channel.ToLower() == channelName.ToLower())
            .ToListAsync();

        foreach (var message in messages)
        {
            message.IsDeleted = true;
        }

        db.Channels.Remove(channel);
        await db.SaveChangesAsync();

        var username = context.User.Claims
            .FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value ?? string.Empty;

        // Notifier tous les clients
        await hubContext.Clients.Group(channelName)
            .SendAsync("ChannelDeleted", channelName, username);

        // Supprimer tout les clients du groupe du canal supprimé
        foreach (var connectedUser in connectedUsers)
        {
            await hubContext.Groups.RemoveFromGroupAsync(connectedUser.ConnectionId, channelName);
        }

        await hubContext.Clients.All
            .SendAsync("ChannelListUpdated");

        logger.LogInformation(
            "Salon {ChannelName} supprimé par {Username}. Messages affectés: {MessageCount}, Utilisateurs déconnectés: {UserCount}",
            channelName, username, messages.Count, connectedUsers.Count);

        return Results.Ok(new
        {
            channelName = channel.Name,
            deletedBy = username,
            messagesAffected = messages.Count,
            usersDisconnected = connectedUsers.Count
        });
    }

    // ========================================================================
    // POST MUTE ENDPOINTS
    // ========================================================================

    private static async Task<IResult> ToggleMuteAsync(
        string channelName,
        ChatDbContext db,
        HttpContext context,
        IHubContext<ChatHub> hubContext,
        ILogger<Program> logger)
    {
        var channel = await db.Channels
            .FirstOrDefaultAsync(c => c.Name.ToLower() == channelName.ToLower());

        if (channel == null)
        {
            logger.LogWarning("Tentative de modification du mode muet d'un salon inexistant: {ChannelName}", channelName);
            return Results.NotFound(new { error = "channel_not_found" });
        }

        var username = context.User.Claims
            .FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value ?? string.Empty;

        channel.IsMuted = !channel.IsMuted;

        if (!channel.IsMuted)
        {
            channel.ActiveManager = username;
        }

        await db.SaveChangesAsync();

        await hubContext.Clients.Group(channelName)
            .SendAsync("ChannelMuteStatusChanged", channelName, channel.IsMuted);

        logger.LogInformation(
            "Mode muet du salon {ChannelName} modifié par {Username}. Nouveau statut: {IsMuted}",
            channelName, username, channel.IsMuted);

        return Results.Ok(new
        {
            channelName = channel.Name,
            isMuted = channel.IsMuted,
            changedBy = username,
            message = channel.IsMuted ? "Le salon est maintenant muet" : "Le salon est de nouveau actif"
        });
    }

    private static async Task<List<Channel>> GetChannelListAsync(IQueryable<ChannelInfo> query)
    {
        return await query.Select(x => new Channel
        {
            Id = x.Channel.Id,
            Name = x.Channel.Name,
            Description = x.Channel.Description,
            CreatedBy = x.Channel.CreatedBy,
            CreatedAt = x.Channel.CreatedAt,
            IsMuted = x.Channel.IsMuted,
            ActiveManager = x.Channel.ActiveManager,
            ConnectedUsersCount = x.ConnectedUsersCount
        })
                        .OrderByDescending(c => c.ConnectedUsersCount)
                        .ThenBy(c => c.Name)
                        .ToListAsync();
    }

    private sealed class ChannelInfo
    {
        public required Channel Channel { get; set; }

        public int ConnectedUsersCount { get; set; }
    }
}