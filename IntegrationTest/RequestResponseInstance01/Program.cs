using IntegrationTest.Common;
using QsMessaging.Public;
using RequestResponseInstance01;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();
builder.Services.AddConfiguredQsMessaging(builder.Configuration);

var host = builder.Build();
await host.UseQsMessaging();
host.Run();
