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
            return GetExchangeNameFromType(typeof(TModel));
        }

        public string GetExchangeNameFromType(Type TModel)
        {
            return GetExchangeNameFromType(TModel, RqExchangePurpose.Permanent);
        }

        public string GetExchangeNameFromType(Type TModel, RqExchangePurpose purpose)
        {
            if (TModel is null)
            {
                throw new ArgumentNullException();
            }

            return purpose switch
            {
                RqExchangePurpose.Permanent => GenerateName(TModel, "ex"),
                RqExchangePurpose.Temporary => GenerateName(TModel, "ex"),
                RqExchangePurpose.TemporaryForResponse => GenerateName(TModel, "ex:livetime:" + instanceService.GetInstanceUID().ToString("N")),
                _ => throw new ArgumentOutOfRangeException(nameof(purpose)),
            };
        }

        public string GetQueueNameFromType(Type TModel, RqQueuePurpose queueType)
        {
            if (TModel is null)
            {
                throw new ArgumentNullException();
            }

            string banseQueueName = $"Qs:{TModel.FullName}";

            switch (queueType)
            {
                case RqQueuePurpose.Permanent:
                    return GenerateName(TModel, "permanent");
                case RqQueuePurpose.ConsumerTemporary:
                    return GenerateName(TModel, Guid.NewGuid().ToString("N"));
                case RqQueuePurpose.InstanceTemporary:
                    return GenerateName(TModel, "livetime:" + instanceService.GetInstanceUID().ToString("N"));
                case RqQueuePurpose.SingleTemporary:
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
