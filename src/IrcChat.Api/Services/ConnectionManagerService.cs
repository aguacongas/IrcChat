using IrcChat.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace IrcChat.Api.Services;
public class ConnectionManagerService(
    IDbContextFactory<ChatDbContext> dbContextFactory,
    ILogger<ConnectionManagerService> logger) : BackgroundService
{
    private readonly string _instanceId = Guid.NewGuid().ToString();
    private const int CLEANUP_INTERVAL_SECONDS = 30;
    private const int USER_TIMEOUT_SECONDS = 60;

    public string GetInstanceId() => _instanceId;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupStaleConnections();
                await Task.Delay(TimeSpan.FromSeconds(CLEANUP_INTERVAL_SECONDS), stoppingToken);
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

        var timeout = DateTime.UtcNow.AddSeconds(-USER_TIMEOUT_SECONDS);
        var staleConnections = await db.ConnectedUsers
            .Where(u => u.LastPing < timeout)
            .ToListAsync();

        if (staleConnections.Any())
        {
            db.ConnectedUsers.RemoveRange(staleConnections);
            await db.SaveChangesAsync();
            logger.LogInformation(
                "Nettoyage des connexions inactives: {count} utilisateurs supprimés",
                staleConnections.Count);
        }
    }
}