# QsMessaging
QsMessaging library for sending and receiving messages between different components of your application using RabbitMQ.

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

	await _qsMessaging.SendMessageAsync(new RegularMessageContract { MyTextMessage = "My message." });

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
