using QsMessaging.Public;
using System.Reflection;

namespace ArrangeInstance01
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);
            builder.Services.AddHostedService<Worker>();
            var azureServiceBusConfiguration = builder.Configuration.GetSection("QsMessaging:AzureServiceBus");

            builder.Services.AddQsMessaging(options =>
            {
                options.Transport = QsMessagingTransport.AzureServiceBus;
                options.AzureServiceBus.ConnectionString =
                    azureServiceBusConfiguration["ConnectionString"]
                    ?? "Endpoint=sb://your-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=YOUR_KEY;";
                options.AzureServiceBus.AdministrationConnectionString = azureServiceBusConfiguration["AdministrationConnectionString"];
                options.AzureServiceBus.EmulatorAmqpPort =
                    azureServiceBusConfiguration.GetValue<int?>("EmulatorAmqpPort")
                    ?? options.AzureServiceBus.EmulatorAmqpPort;
                options.AzureServiceBus.EmulatorManagementPort =
                    azureServiceBusConfiguration.GetValue<int?>("EmulatorManagementPort")
                    ?? options.AzureServiceBus.EmulatorManagementPort;
            });

            // Register all classes that implement IRunScenario
            var assembly = Assembly.GetExecutingAssembly();
            var types = assembly.GetTypes()
                                .Where(t => t.GetInterfaces().Contains(typeof(IScenario)) && t.IsClass);

            foreach (var type in types)
            {
                builder.Services.AddTransient(typeof(IScenario), type);
            }

            var host = builder.Build();

            await host.UseQsMessaging();

            host.Run();
        }
    }
}
