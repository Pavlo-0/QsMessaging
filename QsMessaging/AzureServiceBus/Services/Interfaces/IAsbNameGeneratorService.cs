namespace QsMessaging.AzureServiceBus
{
    internal interface IAsbNameGeneratorService
    {
        string GetAsbQueueNameFromType(Type TModel);

        string GetAsbTopicNameFromType(Type TModel);

        string BuildSubscriptionName(Type TModel);
        //string BuildSubscriptionName(QsMessaging.RabbitMq.Models.HandlersStoreRecord record, Guid instanceUid);
    }
}
