using QsMessaging.Public;

namespace RequestResponse.ResponseInstance
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

            host.Run();
        }
    }
}