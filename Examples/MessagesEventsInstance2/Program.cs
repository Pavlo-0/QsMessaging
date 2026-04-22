using Examples.Common;
using MessagesEventsInstance2;
using MessagesEventsInstance2.Service;
using QsMessaging.Public;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

builder.Services.AddConfiguredQsMessaging(builder.Configuration);
builder.Services.AddScoped<ITestService, TestService>();

var host = builder.Build();

await host.UseQsMessaging();

host.Run();
