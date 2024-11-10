using QsMessaging.Public;

namespace MessagesEventsInstance1
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);
            builder.Services.AddHostedService<Worker>();

            builder.Services.AddQsMessaging();


            var host = builder.Build();
            host.Run();
        }
    }
}