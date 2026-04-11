using QsMessaging.RabbitMq.Models.Enums;

namespace QsMessaging.Shared.Interface
{
    internal interface INameGenerator
    {
        string GetExchangeNameFromType<TModel>();

        string GetExchangeNameFromType(Type TModel);

        string GetQueueNameFromType(Type TModel, QueuePurpose queueType);
    }
}
