using QsMessaging.Public;

namespace AssertInstance01
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);
            builder.Services.AddHostedService<Worker>();

            builder.Services.AddQsMessaging(options => { });

            var host = builder.Build();

            await host.UseQsMessaging();

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