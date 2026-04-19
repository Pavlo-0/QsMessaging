using Microsoft.Extensions.Logging;
using QsMessaging.AzureServiceBus.Services;
using QsMessaging.AzureServiceBus.Services.Interfaces;
using QsMessaging.RabbitMq.Models;
using QsMessaging.Shared.Interface;
using QsMessaging.Shared.Services.Interfaces;

namespace QsMessaging.AzureServiceBus
{
    internal class AsbSubscriber(
        ILogger<AsbSubscriber> logger,
        IAsbServiceBusProcessorService serviceBusProcessorService,
        IHandlerService handlerService,
        IAsbConsumerService handlersService) : ISubscriber
    {

        public async Task SubscribeAsync(CancellationToken cancellationToken = default)
        {
            foreach (var record in handlerService.GetHandlers())
            {
                await SubscribeHandlerAsync(record, cancellationToken);
            }
        }

        public async Task SubscribeHandlerAsync(HandlersStoreRecord record, CancellationToken cancellationToken = default)
        {
            logger.LogInformation("Subscribing handler to the {Type}", record.GenericType.FullName);

            var processor = await serviceBusProcessorService.GetOrCreate(record, cancellationToken);
            processor.ProcessMessageAsync +=  args => handlersService.HandleMessageAsync(args, record, processor.Identifier);
            processor.ProcessErrorAsync += args => handlersService.HandleProcessingErrorAsync(args, processor.Identifier);

            await processor.StartProcessingAsync(cancellationToken);
        }

        public async Task CloseAsync(CancellationToken cancellationToken = default)
        {
            await serviceBusProcessorService.StopAndDisposeProcessorAsync();
        }
    }
}
