using IntegrationTestV2.Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QsMessaging.Public;

namespace IntegrationTestV2.Common;

public sealed class ServiceHeartbeatWorker(
    ServiceIdentity identity,
    IQsMessaging messaging,
    ILogger<ServiceHeartbeatWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await messaging.SendEventAsync(new ServiceHeartbeatEvent(
                    identity.ServiceId,
                    identity.Role,
                    DateTimeOffset.UtcNow));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send heartbeat for {ServiceId}.", identity.ServiceId);
            }

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }
}
