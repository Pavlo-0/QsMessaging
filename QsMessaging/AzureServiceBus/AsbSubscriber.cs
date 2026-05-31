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
        private readonly static ConcurrentBag<AsbProcessorRecord> _processors = new();

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

            var processorRegistration = await serviceBusProcessorService.GetOrCreate(record, cancellationToken);
            var processor = processorRegistration.Processor;
            var processorCancellation = new CancellationTokenSource();


            processor.ProcessMessageAsync += args => handlersService.HandleMessageAsync(args, record, processorRegistration, processorCancellation.Token);
            processor.ProcessErrorAsync += args => handlersService.HandleProcessingErrorAsync(args, processorRegistration.EntityName);

            try
            {
                await processor.StartProcessingAsync(cancellationToken);

                _processors.Add(new AsbProcessorRecord(processor, processorCancellation));
            }
            catch
            {
                processorCancellation.Dispose();
                throw;
            }
        }

        public async Task CloseAsync(CancellationToken cancellationToken = default)
        {
            await Task.WhenAll(_processors.Select(processorRecord => StopAndDisposeProcessorAsync(processorRecord, cancellationToken)));
            _processors.Clear();
        }

        private async Task StopAndDisposeProcessorAsync(AsbProcessorRecord processorRecord, CancellationToken cancellationToken)
        {
            var processor = processorRecord.Processor;
            CancelProcessor(processorRecord.CancellationTokenSource);

            try
            {
                if (!processor.IsClosed)
                {
                    if (processor.IsProcessing)
                    {
                        await processor.StopProcessingAsync(cancellationToken);
                    }

                    await processor.DisposeAsync();
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to stop Azure Service Bus processor cleanly.");
            }
            finally
            {
                processorRecord.CancellationTokenSource.Dispose();
            }
        }

        private static void CancelProcessor(CancellationTokenSource cancellationTokenSource)
        {
            try
            {
                cancellationTokenSource.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private sealed record AsbProcessorRecord(
            ServiceBusProcessor Processor,
            CancellationTokenSource CancellationTokenSource);
    }
}
