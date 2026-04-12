using Examples.Common;
using MessagesEventsInstance2;
using QsMessaging.Public;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

builder.Services.AddConfiguredQsMessaging(builder.Configuration);

var host = builder.Build();

await host.UseQsMessaging();

host.Run();
