using QsMessaging.RabbitMq.Services.Interfaces;
using QsMessaging.Shared;
namespace QsMessaging.AzureServiceBus.Services
{

    internal class AsbNameGenerator(IInstanceService instanceService) : NameGeneratorBase, IAsbNameGeneratorService
    {
        public string GetAsbQueueNameFromType(Type TModel)
        {
            if (TModel is null)
            {
                throw new ArgumentNullException();
            }

            var fullName = TModel.FullName ?? "unknowType";
            return "Qs-Queue-" + (fullName.Length > 200 ? HashString(fullName) : fullName);
        }

        public string GetAsbTopicNameFromType(Type TModel)
        {
            if (TModel is null)
            {
                throw new ArgumentNullException();
            }

            var fullName = TModel.FullName ?? "unknowType";
            return "Qs-Topic-" + (fullName.Length > 200 ? HashString(fullName) : fullName);
        }

        public string BuildSubscriptionName(Type TModel)
        {
            var rawName = $"{TModel.FullName}:{instanceService.GetInstanceUID().ToString("N")}";
            var suffix = HashString(rawName, 45);
            return $"Qs_s{suffix}";
        }

        /*
        public string BuildSubscriptionName(QsMessaging.RabbitMq.Models.HandlersStoreRecord record, Guid instanceUid)
        {
            var rawName = $"{record.GenericType.FullName}:{record.HandlerType.FullName}:{instanceUid:N}";
            var suffix = HashString(rawName, 160);
            return $"Qs:sub:{suffix}";
        }*/
    }
}
