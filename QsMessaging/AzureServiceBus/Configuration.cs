namespace QsMessaging.AzureServiceBus
{
    public class QsAzureServiceBusConfiguration
    {
        public string ConnectionString { get; set; } = string.Empty;

        /// <summary>
        /// Azure Service Bus emulator uses an AMQP port for send/receive operations.
        /// Ignored for cloud namespaces and when the endpoint already specifies a port.
        /// </summary>
        public int EmulatorAmqpPort { get; set; } = 5672;

        /// <summary>
        /// Optional management connection string used for entity creation.
        /// When omitted, QsMessaging uses <see cref="ConnectionString"/> and automatically applies the emulator management port if needed.
        /// </summary>
        public string? AdministrationConnectionString { get; set; }

        /// <summary>
        /// Azure Service Bus emulator uses a dedicated management port for admin operations.
        /// Ignored for cloud namespaces and when the administration endpoint already specifies a port.
        /// </summary>
        public int EmulatorManagementPort { get; set; } = 5300;
    }
}
