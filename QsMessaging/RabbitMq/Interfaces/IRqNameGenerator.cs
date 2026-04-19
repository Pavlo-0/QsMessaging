using QsMessaging.RabbitMq.Models.Enums;

namespace QsMessaging.Shared.Interface
{
    internal interface IRqNameGenerator
    {
        string GetExchangeNameFromType<TModel>();

        string GetExchangeNameFromType(Type TModel);

        string GetQueueNameFromType(Type TModel, RqQueuePurpose queueType);

    }
}
