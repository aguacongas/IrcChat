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
    private readonly ConnectionManagerOptions _options = options.Value;
    private readonly string _instanceId = options.Value.GetInstanceId();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "ConnectionManager démarré - Instance: {InstanceId}, Cleanup: {CleanupInterval}s, Timeout: {UserTimeout}s",
            _instanceId,
            _options.CleanupIntervalSeconds,
            _options.UserTimeoutSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupStaleConnections();
                await Task.Delay(
                    TimeSpan.FromSeconds(_options.CleanupIntervalSeconds),
                    stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erreur lors du nettoyage des connexions");
            }
        }
    }

    private async Task CleanupStaleConnections()
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();

        var timeout = DateTime.UtcNow.AddSeconds(-_options.UserTimeoutSeconds);
        var staleConnections = await db.ConnectedUsers
            .Where(u => u.LastPing < timeout)
            .ToListAsync();

        if (staleConnections.Count != 0)
        {
            db.ConnectedUsers.RemoveRange(staleConnections);
            await db.SaveChangesAsync();
            logger.LogInformation(
                "Nettoyage des connexions inactives: {count} utilisateurs supprimés",
                staleConnections.Count);
        }
    }
}