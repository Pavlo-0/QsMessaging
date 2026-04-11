using Azure.Messaging.ServiceBus;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Collections.Generic;

namespace QsMessaging.AzureServiceBus.Services
{
    internal sealed class ServiceBusConnectionAdapter(ServiceBusClient client) : IConnection
    {
        public ServiceBusClient Client { get; } = client;

        public bool IsOpen { get; private set; } = true;

        public ushort ChannelMax => 0;

        public IDictionary<string, object?> ClientProperties { get; } = new Dictionary<string, object?>();

        public ShutdownEventArgs? CloseReason { get; private set; }

        public AmqpTcpEndpoint Endpoint { get; } = new("localhost");

        public uint FrameMax => 0;

        public TimeSpan Heartbeat => TimeSpan.Zero;

        public IProtocol Protocol => Protocols.DefaultProtocol;

        public IDictionary<string, object?> ServerProperties { get; } = new Dictionary<string, object?>();

        public IEnumerable<ShutdownReportEntry> ShutdownReport { get; } = new List<ShutdownReportEntry>();

        public string ClientProvidedName => nameof(ServiceBusConnectionAdapter);

        public int LocalPort => 0;

        public int RemotePort => 0;

        public event AsyncEventHandler<CallbackExceptionEventArgs>? CallbackExceptionAsync;

        public event AsyncEventHandler<ShutdownEventArgs>? ConnectionShutdownAsync;

        public event AsyncEventHandler<AsyncEventArgs>? RecoverySucceededAsync;

        public event AsyncEventHandler<ConnectionRecoveryErrorEventArgs>? ConnectionRecoveryErrorAsync;

        public event AsyncEventHandler<ConsumerTagChangedAfterRecoveryEventArgs>? ConsumerTagChangeAfterRecoveryAsync;

        public event AsyncEventHandler<QueueNameChangedAfterRecoveryEventArgs>? QueueNameChangedAfterRecoveryAsync;

        public event AsyncEventHandler<RecoveringConsumerEventArgs>? RecoveringConsumerAsync;

        public event AsyncEventHandler<ConnectionBlockedEventArgs>? ConnectionBlockedAsync;

        public event AsyncEventHandler<AsyncEventArgs>? ConnectionUnblockedAsync;

        public Task UpdateSecretAsync(string newSecret, string reason, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Azure Service Bus does not support RabbitMQ secret rotation.");
        }

        public Task CloseAsync(ushort replyCode, string message, TimeSpan timeout, bool abort, CancellationToken cancellationToken = default)
        {
            return DisposeAsync().AsTask();
        }

        public Task<IChannel> CreateChannelAsync(CreateChannelOptions? createChannelOptions = null, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Azure Service Bus does not expose RabbitMQ channels.");
        }

        public async ValueTask DisposeAsync()
        {
            if (!IsOpen)
            {
                return;
            }

            IsOpen = false;
            CloseReason = new ShutdownEventArgs(ShutdownInitiator.Application, 200, "Closed");
            await Client.DisposeAsync();
        }

        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}
