using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QsMessaging.Public;

namespace Examples.Common
{
    public static class ExampleQsMessagingRegistration
    {
        public static IServiceCollection AddConfiguredQsMessaging(this IServiceCollection services, IConfiguration configuration)
        {
            var settings = new ExampleQsMessagingSettings();
            configuration.GetSection("QsMessaging").Bind(settings);

            services.AddQsMessaging(options =>
            {
                options.Transport = settings.Transport;

                options.RabbitMQ.Host = settings.RabbitMQ.Host;
                options.RabbitMQ.UserName = settings.RabbitMQ.UserName;
                options.RabbitMQ.Password = settings.RabbitMQ.Password;
                options.RabbitMQ.Port = settings.RabbitMQ.Port;

                options.AzureServiceBus.ConnectionString = settings.AzureServiceBus.ConnectionString;
                options.AzureServiceBus.AdministrationConnectionString = settings.AzureServiceBus.AdministrationConnectionString;
                options.AzureServiceBus.EmulatorAmqpPort = settings.AzureServiceBus.EmulatorAmqpPort;
                options.AzureServiceBus.EmulatorManagementPort = settings.AzureServiceBus.EmulatorManagementPort;
            });

            return services;
        }
    }

    public sealed class ExampleQsMessagingSettings
    {
        public QsMessagingTransport Transport { get; set; } = QsMessagingTransport.RabbitMq;

        public ExampleRabbitMqSettings RabbitMQ { get; set; } = new();

        public ExampleAzureServiceBusSettings AzureServiceBus { get; set; } = new();
    }

    public sealed class ExampleRabbitMqSettings
    {
        public string Host { get; set; } = "localhost";

        public string UserName { get; set; } = "guest";

        public string Password { get; set; } = "guest";

        public int Port { get; set; } = 5672;
    }

    public sealed class ExampleAzureServiceBusSettings
    {
        public string ConnectionString { get; set; }
            = "Endpoint=sb://your-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=YOUR_KEY;";

        public string? AdministrationConnectionString { get; set; }

        public int EmulatorAmqpPort { get; set; } = 5673;

        public int EmulatorManagementPort { get; set; } = 5300;
    }
}
