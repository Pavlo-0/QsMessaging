using System.Security.Cryptography;
using System.Text;
using QsMessaging.Public;
using TestContract.TransportCleanup;

namespace IntegrationTest.Common;

internal static class TransportCleanupScenarioCatalog
{
    public static TransportCleanupScenarioDefinition CleanUpTransportation { get; } = new(
        ScenarioKey: "cleanup-transportation",
        MessageContractType: typeof(CleanUpTransportMessageContract),
        EventContractType: typeof(CleanUpTransportEventContract),
        RequestContractType: typeof(CleanUpTransportRequestContract),
        ResponseContractType: typeof(CleanUpTransportResponseContract));

    public static TransportCleanupScenarioDefinition FullCleanUpTransportation { get; } = new(
        ScenarioKey: "full-cleanup-transportation",
        MessageContractType: typeof(FullCleanUpTransportMessageContract),
        EventContractType: typeof(FullCleanUpTransportEventContract),
        RequestContractType: typeof(FullCleanUpTransportRequestContract),
        ResponseContractType: typeof(FullCleanUpTransportResponseContract));

    public static IReadOnlyList<TransportCleanupScenarioDefinition> All { get; } =
    [
        CleanUpTransportation,
        FullCleanUpTransportation
    ];
}

internal sealed record TransportCleanupScenarioDefinition(
    string ScenarioKey,
    Type MessageContractType,
    Type EventContractType,
    Type RequestContractType,
    Type ResponseContractType)
{
    public string DirectQueueName => $"it-{ScenarioKey}-direct-queue";

    public string DirectExchangeName => $"it-{ScenarioKey}-direct-exchange";

    public string DirectTopicName => $"it-{ScenarioKey}-direct-topic";

    public string DirectSubscriptionName => $"it-{ScenarioKey}-direct-subscription";
}

internal sealed record TransportEntitySnapshot(
    IReadOnlyCollection<string> QueueNames,
    IReadOnlyCollection<string> ExchangeNames,
    IReadOnlyCollection<string> TopicNames,
    IReadOnlyDictionary<string, IReadOnlyCollection<string>> SubscriptionsByTopic)
{
    public bool HasQueue(string queueName) => QueueNames.Contains(queueName, StringComparer.Ordinal);

    public bool AnyQueue(Func<string, bool> predicate) => QueueNames.Any(predicate);

    public bool HasExchange(string exchangeName) => ExchangeNames.Contains(exchangeName, StringComparer.Ordinal);

    public bool AnyExchange(Func<string, bool> predicate) => ExchangeNames.Any(predicate);

    public bool HasTopic(string topicName) => TopicNames.Contains(topicName, StringComparer.Ordinal);

    public bool HasSubscription(string topicName, string subscriptionName)
    {
        return SubscriptionsByTopic.TryGetValue(topicName, out var subscriptions)
            && subscriptions.Contains(subscriptionName, StringComparer.Ordinal);
    }

    public bool AnySubscription(string topicName, Func<string, bool> predicate)
    {
        return SubscriptionsByTopic.TryGetValue(topicName, out var subscriptions)
            && subscriptions.Any(predicate);
    }
}

internal static class TransportCleanupAssertions
{
    public static bool HasCreatedState(
        TransportEntitySnapshot snapshot,
        QsMessagingTransport transport,
        TransportCleanupScenarioDefinition scenario)
    {
        return transport switch
        {
            QsMessagingTransport.RabbitMq =>
                HasRabbitManagedEntities(snapshot, scenario) &&
                HasRabbitDirectEntities(snapshot, scenario),
            QsMessagingTransport.AzureServiceBus =>
                HasAzureManagedEntities(snapshot, scenario) &&
                HasAzureDirectEntities(snapshot, scenario),
            _ => throw new NotSupportedException($"Transport {transport} is not supported.")
        };
    }

    public static bool HasExpectedStateAfterCleanUp(
        TransportEntitySnapshot snapshot,
        QsMessagingTransport transport,
        TransportCleanupScenarioDefinition scenario)
    {
        return transport switch
        {
            QsMessagingTransport.RabbitMq =>
                !HasRabbitManagedEntities(snapshot, scenario) &&
                HasRabbitDirectEntities(snapshot, scenario),
            QsMessagingTransport.AzureServiceBus =>
                !HasAzureManagedEntities(snapshot, scenario) &&
                HasAzureDirectEntities(snapshot, scenario),
            _ => throw new NotSupportedException($"Transport {transport} is not supported.")
        };
    }

    public static bool HasExpectedStateAfterFullCleanUp(
        TransportEntitySnapshot snapshot,
        QsMessagingTransport transport,
        TransportCleanupScenarioDefinition scenario)
    {
        return transport switch
        {
            QsMessagingTransport.RabbitMq =>
                !HasRabbitManagedEntities(snapshot, scenario) &&
                !HasRabbitDirectEntities(snapshot, scenario),
            QsMessagingTransport.AzureServiceBus =>
                !HasAzureManagedEntities(snapshot, scenario) &&
                !HasAzureDirectEntities(snapshot, scenario),
            _ => throw new NotSupportedException($"Transport {transport} is not supported.")
        };
    }

    public static bool IsRabbitManagedQueue(string queueName, TransportCleanupScenarioDefinition scenario)
    {
        return queueName == TransportCleanupNaming.GetRabbitPermanentQueueName(scenario.MessageContractType)
            || queueName == TransportCleanupNaming.GetRabbitSingleTemporaryQueueName(scenario.RequestContractType)
            || queueName.StartsWith(TransportCleanupNaming.GetRabbitTypePrefix(scenario.EventContractType), StringComparison.Ordinal)
            || queueName.StartsWith(TransportCleanupNaming.GetRabbitResponseQueuePrefix(scenario.ResponseContractType), StringComparison.Ordinal);
    }

    public static bool IsRabbitManagedExchange(string exchangeName, TransportCleanupScenarioDefinition scenario)
    {
        return exchangeName == TransportCleanupNaming.GetRabbitExchangeName(scenario.MessageContractType)
            || exchangeName == TransportCleanupNaming.GetRabbitExchangeName(scenario.EventContractType)
            || exchangeName == TransportCleanupNaming.GetRabbitExchangeName(scenario.RequestContractType)
            || exchangeName.StartsWith(TransportCleanupNaming.GetRabbitResponseExchangePrefix(scenario.ResponseContractType), StringComparison.Ordinal);
    }

    public static bool IsAzureManagedQueue(string queueName, TransportCleanupScenarioDefinition scenario)
    {
        return queueName == TransportCleanupNaming.GetAzureRequestQueueName(scenario.RequestContractType)
            || TransportCleanupNaming.IsAzureResponseQueueName(queueName, scenario.ResponseContractType);
    }

    public static bool IsAzureManagedTopic(string topicName, TransportCleanupScenarioDefinition scenario)
    {
        return topicName == TransportCleanupNaming.GetAzureTopicName(scenario.MessageContractType)
            || topicName == TransportCleanupNaming.GetAzureTopicName(scenario.EventContractType);
    }

    private static bool HasRabbitManagedEntities(TransportEntitySnapshot snapshot, TransportCleanupScenarioDefinition scenario)
    {
        return snapshot.HasQueue(TransportCleanupNaming.GetRabbitPermanentQueueName(scenario.MessageContractType))
            && snapshot.HasExchange(TransportCleanupNaming.GetRabbitExchangeName(scenario.MessageContractType))
            && snapshot.AnyQueue(queueName => queueName.StartsWith(TransportCleanupNaming.GetRabbitTypePrefix(scenario.EventContractType), StringComparison.Ordinal))
            && snapshot.HasExchange(TransportCleanupNaming.GetRabbitExchangeName(scenario.EventContractType))
            && snapshot.HasQueue(TransportCleanupNaming.GetRabbitSingleTemporaryQueueName(scenario.RequestContractType))
            && snapshot.HasExchange(TransportCleanupNaming.GetRabbitExchangeName(scenario.RequestContractType))
            && snapshot.AnyQueue(queueName => queueName.StartsWith(TransportCleanupNaming.GetRabbitResponseQueuePrefix(scenario.ResponseContractType), StringComparison.Ordinal))
            && snapshot.AnyExchange(exchangeName => exchangeName.StartsWith(TransportCleanupNaming.GetRabbitResponseExchangePrefix(scenario.ResponseContractType), StringComparison.Ordinal));
    }

    private static bool HasRabbitDirectEntities(TransportEntitySnapshot snapshot, TransportCleanupScenarioDefinition scenario)
    {
        return snapshot.HasQueue(scenario.DirectQueueName)
            && snapshot.HasExchange(scenario.DirectExchangeName);
    }

    private static bool HasAzureManagedEntities(TransportEntitySnapshot snapshot, TransportCleanupScenarioDefinition scenario)
    {
        var messageTopicName = TransportCleanupNaming.GetAzureTopicName(scenario.MessageContractType);
        var eventTopicName = TransportCleanupNaming.GetAzureTopicName(scenario.EventContractType);

        return snapshot.HasTopic(messageTopicName)
            && snapshot.HasSubscription(messageTopicName, TransportCleanupNaming.GetAzurePermanentSubscriptionName(scenario.MessageContractType))
            && snapshot.HasTopic(eventTopicName)
            && snapshot.AnySubscription(eventTopicName, TransportCleanupNaming.IsAzureTemporarySubscriptionName)
            && snapshot.HasQueue(TransportCleanupNaming.GetAzureRequestQueueName(scenario.RequestContractType))
            && snapshot.AnyQueue(queueName => TransportCleanupNaming.IsAzureResponseQueueName(queueName, scenario.ResponseContractType));
    }

    private static bool HasAzureDirectEntities(TransportEntitySnapshot snapshot, TransportCleanupScenarioDefinition scenario)
    {
        return snapshot.HasQueue(scenario.DirectQueueName)
            && snapshot.HasTopic(scenario.DirectTopicName)
            && snapshot.HasSubscription(scenario.DirectTopicName, scenario.DirectSubscriptionName);
    }
}

internal static class TransportCleanupNaming
{
    public static string GetRabbitTypePrefix(Type contractType) => $"Qs:{GetSafeTypePart(contractType)}:";

    public static string GetRabbitPermanentQueueName(Type contractType) => $"{GetRabbitTypePrefix(contractType)}permanent";

    public static string GetRabbitSingleTemporaryQueueName(Type contractType) => $"{GetRabbitTypePrefix(contractType)}livetime";

    public static string GetRabbitExchangeName(Type contractType) => $"{GetRabbitTypePrefix(contractType)}ex";

    public static string GetRabbitResponseQueuePrefix(Type contractType) => $"{GetRabbitTypePrefix(contractType)}livetime:";

    public static string GetRabbitResponseExchangePrefix(Type contractType) => $"{GetRabbitTypePrefix(contractType)}ex:livetime:";

    public static string GetAzureTopicName(Type contractType) => $"Qs-Topic-{GetSafeTypePart(contractType)}";

    public static string GetAzureRequestQueueName(Type contractType) => $"Qs-Q-Request-{GetSafeTypePart(contractType)}";

    public static bool IsAzureResponseQueueName(string queueName, Type contractType)
    {
        return queueName.StartsWith("Qs-Q-Response-", StringComparison.Ordinal)
            && queueName.EndsWith($"-{GetSafeTypePart(contractType)}", StringComparison.Ordinal);
    }

    public static string GetAzurePermanentSubscriptionName(Type contractType)
    {
        return $"Qs_P_{HashString($"{contractType.FullName}_", 42)}";
    }

    public static bool IsAzureTemporarySubscriptionName(string subscriptionName)
    {
        return subscriptionName.StartsWith("Qs_T_", StringComparison.Ordinal);
    }

    public static string GetSafeTypePart(Type contractType)
    {
        var fullName = contractType.FullName ?? "unknownType";
        return fullName.Length > 200 ? HashString(fullName) : fullName;
    }

    private static string HashString(string input, int maxLength = 200)
    {
        using var sha256 = SHA256.Create();
        var inputBytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = sha256.ComputeHash(inputBytes);
        var hashString = BitConverter.ToString(hashBytes).Replace("-", string.Empty).ToLowerInvariant();

        return hashString.Length > maxLength ? hashString[..maxLength] : hashString;
    }
}
