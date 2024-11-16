# QsMessaging
QsMessaging library for sending and receiving messages between different services (components) of your application using __RabbitMQ__.
[![NuGet](https://img.shields.io/nuget/v/QsMessaging.svg)](https://www.nuget.org/packages/QsMessaging/)  


A .NET Core 8 library for messaging services.

## Installation

	dotnet add package QsMessaging

## Registering
Registering the library is simple. You just need to add two lines of code.

	...

	var builder = Host.CreateApplicationBuilder(args);

	....
	// Add QsMessaging (leave the options empty for the default configuration)...
	builder.Services.AddQsMessaging(options => { });

	...

	var host = builder.Build();

	//...and use it
	await host.UseQsMessaging();

	host.Run();



### Default configuration
**RabbitMQ**

Host = "localhost"  
UserName = "guest"  
Password = "guest"  
Port = 5672

## Using

### Sending messages

#### Contract
	public class RegularMessageContract
	{
		public required string MyTextMessage { get; set; } 
	}

#### Send
Inject in your class:   

    public YouClass(IQsMessaging qsMessaging) {}

Then you may use it

	await qsMessaging.SendMessageAsync(new RegularMessageContract { MyTextMessage = "My message." });

#### Handle message

    public class RegularMessageContractHandler : IQsMessageHandler<RegularMessageContract>
    {
        public Task<bool> Consumer(RegularMessageContract contractModel)
        {
		//... You Message here
            return Task.FromResult(true);
        }
    }

**That's all folks**
