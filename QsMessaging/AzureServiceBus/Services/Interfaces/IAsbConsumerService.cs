using Azure.Messaging.ServiceBus;
using QsMessaging.AzureServiceBus.Services;
using QsMessaging.Shared.Models;

namespace QsMessaging.AzureServiceBus.Services.Interfaces
{
    internal interface IAsbConsumerService
    {
        Task HandleMessageAsync(
            ProcessMessageEventArgs args,
            HandlersStoreRecord record,
            AsbProcessorRegistration processorRegistration,
            CancellationToken cancellationToken);

        Task HandleProcessingErrorAsync(ProcessErrorEventArgs args, string entityDisplayName);
    }
}
