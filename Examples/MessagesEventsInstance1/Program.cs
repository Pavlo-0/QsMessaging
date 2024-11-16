using QsMessaging.Public;

namespace MessagesEventsInstance1
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);
            builder.Services.AddHostedService<Worker>();

            // Bind RabbitMQ settings from configuration
            var configuration = builder.Configuration;
            var rabbitMQSettings = new RabbitMQSettings();
            configuration.GetSection("RabbitMQ").Bind(rabbitMQSettings);

            // Pass RabbitMQ settings to AddQsMessaging
            builder.Services.AddQsMessaging(options =>
            {
                options.RabbitMQ.Host = rabbitMQSettings.Host;
                options.RabbitMQ.UserName = rabbitMQSettings.UserName;
                options.RabbitMQ.Password = rabbitMQSettings.Password;
                options.RabbitMQ.Port = rabbitMQSettings.Port;
            });

            var host = builder.Build();

            await host.UseQsMessaging();
            
            host.Run();
        }
    }
    public class RabbitMQSettings
    {
        public string Host { get; set; } = "localhost";
        public string UserName { get; set; } = "guest"; // Default RabbitMQ user
        public string Password { get; set; } = "guest"; // Default RabbitMQ password

        public int Port { get; set; } = 5672; // Default RabbitMQ port
    }
}