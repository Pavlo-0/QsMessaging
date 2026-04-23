using Azure.Messaging.ServiceBus;
using QsMessaging.Shared.Models;

namespace QsMessaging.AzureServiceBus.Services.Interfaces
{
    internal interface IAsbConsumerService
    {
        Task HandleMessageAsync(ProcessMessageEventArgs args, HandlersStoreRecord record, string entityDisplayName, CancellationToken cancellationToken);
        Task HandleProcessingErrorAsync(ProcessErrorEventArgs args, string entityDisplayName);
    }
}