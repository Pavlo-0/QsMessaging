using QsMessaging.Public;
using RequestResponseInstance02;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();
builder.Services.AddQsMessaging(options => { });

var host = builder.Build();
await host.UseQsMessaging();
host.Run();
