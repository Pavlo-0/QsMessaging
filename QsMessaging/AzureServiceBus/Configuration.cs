namespace QsMessaging.AzureServiceBus
{
    public class QsAzureServiceBusConfiguration
    {
        public string ConnectionString { get; set; } = string.Empty;

        /// <summary>
        /// Optional management connection string used for entity creation.
        /// When omitted, QsMessaging uses <see cref="ConnectionString"/> and automatically applies the emulator management port if needed.
        /// </summary>
        public string? AdministrationConnectionString { get; set; }

        /// <summary>
        /// Azure Service Bus emulator uses a dedicated management port for admin operations.
        /// Ignored for cloud namespaces and when <see cref="AdministrationConnectionString"/> is provided.
        /// </summary>
        public int EmulatorManagementPort { get; set; } = 5300;
    }
}
