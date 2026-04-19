using Azure.Messaging.ServiceBus;
using QsMessaging.RabbitMq.Models;

namespace QsMessaging.AzureServiceBus.Services
{
    internal interface IServiceBusProcessorService
    {
        Task<ServiceBusProcessor> GetOrCreate(HandlersStoreRecord record, CancellationToken cancellationToken);
        Task StopAndDisposeProcessorAsync();
    }
}