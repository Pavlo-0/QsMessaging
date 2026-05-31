using IntegrationTestV2.Contracts;
using QsMessaging.Public.Handler;

namespace IntegrationTestV2.Runner;

public sealed class ServiceHeartbeatHandler(SuiteState state) : IQsEventHandler<ServiceHeartbeatEvent>
{
    public Task Consumer(ServiceHeartbeatEvent heartbeat)
    {
        state.RecordHeartbeat(heartbeat);
        return Task.CompletedTask;
    }
}
