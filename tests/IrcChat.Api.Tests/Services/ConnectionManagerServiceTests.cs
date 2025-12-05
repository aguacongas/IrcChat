// tests/IrcChat.Api.Tests/Services/ConnectionManagerServiceTests.cs
using IrcChat.Api.Data;
using IrcChat.Api.Services;
using IrcChat.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace IrcChat.Api.Tests.Services;

public class ConnectionManagerServiceTests : IAsyncDisposable
{
    private readonly PooledDbContextFactory<ChatDbContext> dbContextFactory;
    private readonly Mock<ILogger<ConnectionManagerService>> loggerMock;
    private readonly IOptions<ConnectionManagerOptions> options;

    public ConnectionManagerServiceTests()
    {
        var optionsBuilder = new DbContextOptionsBuilder<ChatDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString());

        dbContextFactory = new PooledDbContextFactory<ChatDbContext>(optionsBuilder.Options);

        loggerMock = new Mock<ILogger<ConnectionManagerService>>();

        options = Options.Create(new ConnectionManagerOptions
        {
            InstanceId = "test-instance",
            CleanupIntervalSeconds = 1,
            UserTimeoutSeconds = 30,
        });
    }

    [Fact]
    public async Task CleanupStaleConnections_ShouldRemoveExpiredConnections()
    {
        // Arrange
        await using var context = await dbContextFactory.CreateDbContextAsync();

        var staleUser = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            Username = "stale_user",
            ConnectionId = "conn_1",
            Channel = "test",
            ConnectedAt = DateTime.UtcNow.AddMinutes(-10),
            LastActivity = DateTime.UtcNow.AddMinutes(-10),
            ServerInstanceId = "test-instance",
        };

        var activeUser = new ConnectedUser
        {
            Id = Guid.NewGuid(),
            Username = "active_user",
            ConnectionId = "conn_2",
            Channel = "test",
            ConnectedAt = DateTime.UtcNow,
            LastActivity = DateTime.UtcNow,
            ServerInstanceId = "test-instance",
        };

        context.ConnectedUsers.AddRange(staleUser, activeUser);
        await context.SaveChangesAsync();

        var service = new ConnectionManagerService(
            dbContextFactory,
            options,
            loggerMock.Object);

        // Act
        await service.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(3)); // Attendre le cleanup
        await service.StopAsync(CancellationToken.None);

        // Assert
        await using var verifyContext = await dbContextFactory.CreateDbContextAsync();
        var remainingUsers = await verifyContext.ConnectedUsers.ToListAsync();

        Assert.Single(remainingUsers);
        Assert.Equal("active_user", remainingUsers[0].Username);
    }

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        await Task.CompletedTask;
    }
}