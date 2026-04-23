using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using QsMessaging.AzureServiceBus.Services;
using QsMessaging.AzureServiceBus.Services.Interfaces;
using QsMessaging.Shared.Interface;
using QsMessaging.Shared.Models;
using QsMessaging.Shared.Services.Interfaces;
using System.Collections.Concurrent;

namespace QsMessaging.AzureServiceBus
{
    internal class AsbSubscriber(
        ILogger<AsbSubscriber> logger,
        IAsbServiceBusProcessorService serviceBusProcessorService,
        IHandlerService handlerService,
        IAsbConsumerService handlersService) : ISubscriber
    {
        private readonly static ConcurrentBag<ServiceBusProcessor> _processors = new();

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


            processor.ProcessMessageAsync += args => handlersService.HandleMessageAsync(args, record, processor.Identifier, cancellationToken);
            processor.ProcessErrorAsync += args => handlersService.HandleProcessingErrorAsync(args, processor.Identifier);

            await processor.StartProcessingAsync(cancellationToken);

            _processors.Add(processor);
        }

        public async Task CloseAsync(CancellationToken cancellationToken = default)
        {
            await Task.WhenAll(_processors.Select(StopAndDisposeProcessorAsync));
            _processors.Clear();
        }

        private async Task StopAndDisposeProcessorAsync(ServiceBusProcessor processor)
        {
            try
            {
                if (!processor.IsClosed)
                {
                    if (processor.IsProcessing)
                    {
                        await processor.StopProcessingAsync(CancellationToken.None);
                    }

                    await processor.DisposeAsync();
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to stop Azure Service Bus processor cleanly.");
            }
        }
    }
}
