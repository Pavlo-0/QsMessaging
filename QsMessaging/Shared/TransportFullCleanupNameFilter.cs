namespace QsMessaging.Shared
{
    internal static class TransportFullCleanupNameFilter
    {
        public const string RabbitMqEntityPrefix = "Qs:";
        public const string AzureServiceBusEntityPrefix = "Qs-";
        public const string AzureServiceBusSubscriptionPrefix = "Qs_";

        private const string RabbitMqReservedExchangePrefix = "amq.";

        public static bool CanDeleteRabbitMqQueue(string queueName, bool allowDangerousFullCleanup)
        {
            return IsNotBlank(queueName)
                && (allowDangerousFullCleanup || IsRabbitMqQsMessagingEntity(queueName));
        }

        public static bool CanDeleteRabbitMqExchange(string exchangeName, bool allowDangerousFullCleanup)
        {
            return IsNotBlank(exchangeName)
                && !exchangeName.StartsWith(RabbitMqReservedExchangePrefix, StringComparison.OrdinalIgnoreCase)
                && (allowDangerousFullCleanup || IsRabbitMqQsMessagingEntity(exchangeName));
        }

        public static bool CanDeleteAzureServiceBusQueueOrTopic(string entityName, bool allowDangerousFullCleanup)
        {
            return IsNotBlank(entityName)
                && (allowDangerousFullCleanup || IsAzureServiceBusQsMessagingEntity(entityName));
        }

        public static bool CanDeleteAzureServiceBusSubscription(string subscriptionName, bool allowDangerousFullCleanup)
        {
            return IsNotBlank(subscriptionName)
                && (allowDangerousFullCleanup || IsAzureServiceBusQsMessagingSubscription(subscriptionName));
        }

        private static bool IsRabbitMqQsMessagingEntity(string entityName)
        {
            return entityName.StartsWith(RabbitMqEntityPrefix, StringComparison.Ordinal);
        }

        private static bool IsAzureServiceBusQsMessagingEntity(string entityName)
        {
            return entityName.StartsWith(AzureServiceBusEntityPrefix, StringComparison.Ordinal);
        }

        private static bool IsAzureServiceBusQsMessagingSubscription(string subscriptionName)
        {
            return subscriptionName.StartsWith(AzureServiceBusSubscriptionPrefix, StringComparison.Ordinal);
        }

        private static bool IsNotBlank(string value)
        {
            return !string.IsNullOrWhiteSpace(value);
        }
    }
}
