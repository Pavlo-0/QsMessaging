using QsMessaging.RabbitMq.Models.Enums;
using QsMessaging.Shared;
using QsMessaging.Shared.Interface;
using QsMessaging.Shared.Services.Interfaces;

namespace QsMessaging.RabbitMq
{
    internal class RqNameGenerator(IInstanceService instanceService): NameGeneratorBase , IRqNameGenerator
    {
        public string GetExchangeNameFromType<TModel>()
        {
            return GenerateName(typeof(TModel), "ex");
        }

        public string GetExchangeNameFromType(Type TModel)
        {
            if (TModel is null)
            {
                throw new ArgumentNullException();
            }

            return GenerateName(TModel, "ex");
        }

        public string GetQueueNameFromType(Type TModel, QueuePurpose queueType)
        {
            if (TModel is null)
            {
                throw new ArgumentNullException();
            }

            string banseQueueName = $"Qs:{TModel.FullName}";

            switch (queueType)
            {
                case QueuePurpose.Permanent:
                    return GenerateName(TModel, "permanent");
                case QueuePurpose.ConsumerTemporary:
                    return GenerateName(TModel, Guid.NewGuid().ToString("N"));
                case QueuePurpose.InstanceTemporary:
                    return GenerateName(TModel, "livetime:" + instanceService.GetInstanceUID().ToString("N"));
                case QueuePurpose.SingleTemporary:
                    return GenerateName(TModel, "livetime");
                default:
                    throw new ArgumentOutOfRangeException("Unknown QueueType");
            }
        }

        private string GenerateName(Type type, string endName = "")
        {
            var fullName = type.FullName ?? "unknowType";
            return "Qs:" + (fullName.Length > 200 ? HashString(fullName) : fullName) + ":" + endName;
        }
    }
}