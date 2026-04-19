using QsMessaging.AzureServiceBus.Models.Enums;

namespace QsMessaging.AzureServiceBus
{
    internal interface IAsbNameGeneratorService
    {
        string GetAsbQueueNameFromType(Type TModel, AsbQueuePurpose queuePurpose);

        string GetAsbTopicNameFromType(Type TModel);

        string GetSubscriptionName(Type TModel, AbsSubscriptionPurpose subscriptionPurpose);
        //string BuildSubscriptionName(QsMessaging.RabbitMq.Models.HandlersStoreRecord record, Guid instanceUid);
    }
}
