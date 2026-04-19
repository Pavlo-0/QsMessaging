using QsMessaging.AzureServiceBus.Models.Enums;
using QsMessaging.Shared;
using QsMessaging.Shared.Services.Interfaces;
namespace QsMessaging.AzureServiceBus.Services
{

    internal class AsbNameGenerator(IInstanceService instanceService) : NameGeneratorBase, IAsbNameGeneratorService
    {
        public string GetAsbQueueNameFromType(Type modelType, AsbQueuePurpose queuePurpose)
        {
            var fullName = modelType.FullName ?? "unknownType";
            var safeTypePart = fullName.Length > 200 ? HashString(fullName) : fullName;

            return queuePurpose switch
            {
                AsbQueuePurpose.Request =>
                    $"Qs-Q-Request-{safeTypePart}",

                AsbQueuePurpose.Response =>
                    $"Qs-Q-Response-{instanceService.GetInstanceUID():N}-{safeTypePart}",

                _ => throw new ArgumentOutOfRangeException(nameof(queuePurpose), queuePurpose, "Unsupported queue purpose.")
            };
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

        public string GetSubscriptionName(Type TModel, AbsSubscriptionPurpose subscriptionPurpose)
        {
            var rawName = $"{TModel.FullName}_";
            var prefix = subscriptionPurpose switch
            {
                AbsSubscriptionPurpose.Permanent => "P",
                AbsSubscriptionPurpose.Temporary => "T",
                _ => "U"
            };
            switch (subscriptionPurpose)
            {
                case AbsSubscriptionPurpose.Permanent:
                    break;
                case AbsSubscriptionPurpose.Temporary:
                    rawName += $"{instanceService.GetInstanceUID().ToString("N")}";
                    break;
                default:
                    break;
            }

            
            var suffix = HashString(rawName, 42);
            return $"Qs_{prefix}_{suffix}";
        }
    }
}
