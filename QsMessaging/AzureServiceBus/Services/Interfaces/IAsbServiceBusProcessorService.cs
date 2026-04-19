using Azure.Messaging.ServiceBus;
using QsMessaging.Shared.Models;

namespace QsMessaging.AzureServiceBus.Services
{
    internal interface IAsbServiceBusProcessorService
    {
        Task<ServiceBusProcessor> GetOrCreate(HandlersStoreRecord record, CancellationToken cancellationToken);
        Task StopAndDisposeProcessorAsync();
    }
}