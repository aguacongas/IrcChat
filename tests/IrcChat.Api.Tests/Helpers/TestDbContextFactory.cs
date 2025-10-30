// tests/IrcChat.Api.Tests/Helpers/TestDbContextFactory.cs
using IrcChat.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace IrcChat.Api.Tests.Helpers;

public static class TestDbContextFactory
{
    public static ChatDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<ChatDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new ChatDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    public static async Task<ChatDbContext> CreateInMemoryContextWithDataAsync()
    {
        var context = CreateInMemoryContext();

        // Ajouter des données de test
        var admin = new IrcChat.Shared.Models.Admin
        {
            Id = Guid.NewGuid(),
            Username = "testadmin",
            PasswordHash = Convert.ToBase64String(
                System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes("password123"))),
            CreatedAt = DateTime.UtcNow
        };

        var channel = new IrcChat.Shared.Models.Channel
        {
            Id = Guid.NewGuid(),
            Name = "general",
            CreatedBy = "system",
            CreatedAt = DateTime.UtcNow
        };

        context.Admins.Add(admin);
        context.Channels.Add(channel);
        await context.SaveChangesAsync();

        return context;
    }
}