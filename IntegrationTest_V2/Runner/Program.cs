using System.Reflection;
using IntegrationTestV2.Common;
using IntegrationTestV2.Contracts;
using IntegrationTestV2.Runner;
using QsMessaging.Public;

var builder = Host.CreateApplicationBuilder(args);
var runnerOptions = new RunnerOptions();
builder.Configuration.GetSection("Runner").Bind(runnerOptions);

builder.Services.AddSingleton(new ServiceIdentity(ServiceIds.Runner, "runner"));
builder.Services.AddSingleton(runnerOptions);
builder.Services.AddSingleton<IssueLog>();
builder.Services.AddSingleton<SuiteState>();
builder.Services.AddSingleton<Dashboard>();
builder.Services.AddSingleton<SenderResultInbox>();
builder.Services.AddHostedService<IntegrationSuiteWorker>();
builder.Services.AddIntegrationTestV2Messaging(
    builder.Configuration,
    Assembly.GetEntryAssembly() ?? throw new InvalidOperationException("Entry assembly is unavailable."));

var host = builder.Build();
await host.UseQsMessaging();
await host.RunAsync();
