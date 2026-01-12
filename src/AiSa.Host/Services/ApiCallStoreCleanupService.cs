using Microsoft.Extensions.Hosting;

namespace AiSa.Host.Services;

/// <summary>
/// Background service that periodically cleans up expired sessions from the API call store.
/// </summary>
public class ApiCallStoreCleanupService : BackgroundService
{
    private readonly IApiCallStore _store;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(5);
    private readonly TimeSpan _sessionTtl = TimeSpan.FromHours(1);

    public ApiCallStoreCleanupService(IApiCallStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _store.CleanupExpiredSessions(_sessionTtl);
            }
            catch (Exception)
            {
                // Log error if needed, but don't fail the service
            }

            await Task.Delay(_cleanupInterval, stoppingToken);
        }
    }
}
