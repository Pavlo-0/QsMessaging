using System.Reflection;
using IntegrationTestV2.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using QsMessaging.Public;

namespace IntegrationTestV2.Sender;

public static class SenderHost
{
    public static async Task RunAsync(string[] args, string senderId)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Services.AddSingleton(new ServiceIdentity(senderId, "sender"));
        builder.Services.AddSingleton<SenderCommandExecutor>();
        builder.Services.AddHostedService<ServiceHeartbeatWorker>();
        builder.Services.AddIntegrationTestV2Messaging(
            builder.Configuration,
            Assembly.GetEntryAssembly() ?? throw new InvalidOperationException("Entry assembly is unavailable."));

        var host = builder.Build();
        await host.UseQsMessaging();
        await host.RunAsync();
    }
}
