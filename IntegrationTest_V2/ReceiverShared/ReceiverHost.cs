using System.Reflection;
using IntegrationTestV2.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using QsMessaging.Public;

namespace IntegrationTestV2.Receiver;

public static class ReceiverHost
{
    public static async Task RunAsync(string[] args, string receiverId)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Services.AddSingleton(new ServiceIdentity(receiverId, "receiver"));
        builder.Services.AddHostedService<ServiceHeartbeatWorker>();
        builder.Services.AddIntegrationTestV2Messaging(
            builder.Configuration,
            Assembly.GetEntryAssembly() ?? throw new InvalidOperationException("Entry assembly is unavailable."));

        var host = builder.Build();
        await host.UseQsMessaging();
        await host.RunAsync();
    }
}
