using QsMessaging.Public;
using System.Reflection;

namespace AssertInstance01
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);
            builder.Services.AddHostedService<Worker>();

            builder.Services.AddQsMessaging(options => { });

            // Register all classes that implement IRunScenario
            var assembly = Assembly.GetExecutingAssembly();
            var types = assembly.GetTypes()
                                .Where(t => t.GetInterfaces().Contains(typeof(IScenario)) && t.IsClass);

            foreach (var type in types)
            {
                builder.Services.AddTransient(typeof(IScenario), type);
            }

            var host = builder.Build();

            // Enum iteration
            foreach (TestScenariousEnum value in Enum.GetValues(typeof(TestScenariousEnum)))
            {
                CollectionTestResults.AddTest(value);
            }

            await host.UseQsMessaging();

            host.Run();
        }
    }
}