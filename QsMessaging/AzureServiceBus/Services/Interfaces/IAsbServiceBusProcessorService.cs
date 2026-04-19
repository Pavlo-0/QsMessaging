using Azure.Messaging.ServiceBus;
using QsMessaging.RabbitMq.Models;

namespace QsMessaging.AzureServiceBus.Services
{
    internal interface IAsbServiceBusProcessorService
    {
        Task<ServiceBusProcessor> GetOrCreate(HandlersStoreRecord record, CancellationToken cancellationToken);
        Task StopAndDisposeProcessorAsync();
    }
}