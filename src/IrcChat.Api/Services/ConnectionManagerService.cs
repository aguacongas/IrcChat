using IrcChat.Api.Data;
using IrcChat.Api.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IrcChat.Api.Services;

public class ConnectionManagerService(
    IDbContextFactory<ChatDbContext> dbContextFactory,
    IOptions<ConnectionManagerOptions> options,
    ILogger<ConnectionManagerService> logger) : BackgroundService
{
    private readonly ConnectionManagerOptions options = options.Value;
    private readonly string instanceId = options.Value.GetInstanceId();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "ConnectionManager démarré - Instance: {InstanceId}, Cleanup: {CleanupInterval}s, Timeout: {UserTimeout}s",
            instanceId,
            options.CleanupIntervalSeconds,
            options.UserTimeoutSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupStaleConnections(stoppingToken);
                await Task.Delay(
                    TimeSpan.FromSeconds(options.CleanupIntervalSeconds),
                    stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erreur lors du nettoyage des connexions");
            }
        }
    }

    private async Task CleanupStaleConnections(CancellationToken stoppingToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(stoppingToken);

        var timeout = DateTime.UtcNow.AddSeconds(-options.UserTimeoutSeconds);
        var staleConnections = await db.ConnectedUsers
            .GroupBy(u => u.ConnectionId)
            .Select(g => new
            {
                ConnectionId = g.Key,
                LastActivity = g.Max(u => u.LastActivity),
            })
            .Where(u => u.LastActivity < timeout)
            .Select(g => g.ConnectionId)
            .ToListAsync(stoppingToken);

        if (staleConnections.Count != 0)
        {
            var connectionsToRemove = await db.ConnectedUsers
                .Where(u => staleConnections.Contains(u.ConnectionId))
                .ToListAsync(stoppingToken);
            db.ConnectedUsers.RemoveRange(connectionsToRemove);
            await db.SaveChangesAsync(stoppingToken);
            logger.LogInformation(
                "Nettoyage des connexions inactives: {Count} utilisateurs supprimés",
                staleConnections.Count);
        }
    }
}