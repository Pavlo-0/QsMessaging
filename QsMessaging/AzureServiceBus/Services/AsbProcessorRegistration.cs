using Azure.Messaging.ServiceBus;

namespace QsMessaging.AzureServiceBus.Services
{
    internal sealed record AsbProcessorRegistration(
        ServiceBusProcessor Processor,
        string EntityName,
        string? DestinationName,
        string? SubscriptionName);
}
