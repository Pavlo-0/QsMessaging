using QsMessaging.RabbitMq.Interface;

namespace QsMessaging.RabbitMq
{
    internal class NameGenerator : INameGenerator
    {
        public string GetExchangeNameFromType<TModel>()
        {
            return GenerateName(typeof(TModel));
        }

        public string GetExchangeNameFromType(Type TModel)
        {
            return GenerateName(TModel);
        }

        public string GetQueueNameFromType(Type TModel, QueueType queueType)
        {
            string banseQueueName = $"q_{TModel.FullName}";

            switch (queueType)
            {
                case QueueType.Permanent:
                        return $"{banseQueueName}:permanent";
                case QueueType.Temporary:
                    return $"{banseQueueName}:{Guid.NewGuid().ToString("N")}";
                default:
                    throw new ArgumentOutOfRangeException("Unknown QueueType");
            }
        }

        private string GenerateName(Type type)
        {
            return "ex_" + type.FullName;
        }
    }

    internal enum QueueType
    {
        Permanent,
        Temporary
    }
}
