using Azure.Messaging.ServiceBus;
using QsMessaging.RabbitMq.Models;

namespace QsMessaging.AzureServiceBus.Services.Interfaces
{
    internal interface IAsbHandlersService
    {
        Task HandleMessageAsync(ProcessMessageEventArgs args, HandlersStoreRecord record, string entityDisplayName);
        Task HandleProcessingErrorAsync(ProcessErrorEventArgs args, string entityDisplayName);
    }
}