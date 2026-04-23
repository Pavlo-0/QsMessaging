using QsMessaging.RabbitMq.Models.Enums;

namespace QsMessaging.Shared.Interface
{
    internal interface IRqNameGenerator
    {
        string GetExchangeNameFromType<TModel>();

        string GetExchangeNameFromType(Type TModel);

        string GetExchangeNameFromType(Type TModel, RqExchangePurpose purpose);

        string GetQueueNameFromType(Type TModel, RqQueuePurpose queueType);

    }
}
