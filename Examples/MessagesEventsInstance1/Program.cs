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
                options.Host = rabbitMQSettings.Host;
                options.UserName = rabbitMQSettings.UserName;
                options.Password = rabbitMQSettings.Password;
            });

            var host = builder.Build();

            await host.UseQsMessaging();
            
            host.Run();
        }
    }
    public class RabbitMQSettings
    {
        public string Host { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
    }
}