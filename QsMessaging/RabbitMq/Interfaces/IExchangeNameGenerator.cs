namespace QsMessaging.RabbitMq.Interface
{
    internal interface IExchangeNameGenerator
    {
        string GetExchangeNameFromType<TModel>();

        string GetExchangeNameFromType(Type TModel);

        string GetQueueNameFromType(Type TModel);

        string GetQueueTemporaryNameFromType(Type TModel);
    }
}
