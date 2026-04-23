using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QsMessaging.Public;
using RabbitMQ.Client;

namespace IntegrationTest.Common;

internal interface ITransportCleanupTestAdministration
{
    QsMessagingTransport Transport { get; }

    Task PrepareScenarioAsync(TransportCleanupScenarioDefinition scenario, CancellationToken cancellationToken = default);

    Task CreateDirectEntitiesAsync(TransportCleanupScenarioDefinition scenario, CancellationToken cancellationToken = default);

    Task<TransportEntitySnapshot> CaptureAsync(CancellationToken cancellationToken = default);
}

internal sealed class TransportCleanupTestAdministration(
    IConfiguration configuration,
    ILogger<TransportCleanupTestAdministration> logger) : ITransportCleanupTestAdministration
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IntegrationTestQsMessagingSettings _settings = BindSettings(configuration);

    public QsMessagingTransport Transport => _settings.Transport;

    public Task PrepareScenarioAsync(TransportCleanupScenarioDefinition scenario, CancellationToken cancellationToken = default)
    {
        return Transport switch
        {
            QsMessagingTransport.RabbitMq => PrepareRabbitScenarioAsync(scenario, cancellationToken),
            QsMessagingTransport.AzureServiceBus => PrepareAzureScenarioAsync(scenario, cancellationToken),
            _ => throw new NotSupportedException($"Transport {Transport} is not supported.")
        };
    }

    public Task CreateDirectEntitiesAsync(TransportCleanupScenarioDefinition scenario, CancellationToken cancellationToken = default)
    {
        return Transport switch
        {
            QsMessagingTransport.RabbitMq => CreateRabbitDirectEntitiesAsync(scenario, cancellationToken),
            QsMessagingTransport.AzureServiceBus => CreateAzureDirectEntitiesAsync(scenario, cancellationToken),
            _ => throw new NotSupportedException($"Transport {Transport} is not supported.")
        };
    }

    public Task<TransportEntitySnapshot> CaptureAsync(CancellationToken cancellationToken = default)
    {
        return Transport switch
        {
            QsMessagingTransport.RabbitMq => CaptureRabbitSnapshotAsync(cancellationToken),
            QsMessagingTransport.AzureServiceBus => CaptureAzureSnapshotAsync(cancellationToken),
            _ => throw new NotSupportedException($"Transport {Transport} is not supported.")
        };
    }

    private async Task PrepareRabbitScenarioAsync(TransportCleanupScenarioDefinition scenario, CancellationToken cancellationToken)
    {
        var snapshot = await CaptureRabbitSnapshotAsync(cancellationToken);
        var queueNames = snapshot.QueueNames
            .Where(queueName => queueName == scenario.DirectQueueName || TransportCleanupAssertions.IsRabbitManagedQueue(queueName, scenario))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var exchangeNames = snapshot.ExchangeNames
            .Where(exchangeName => exchangeName == scenario.DirectExchangeName || TransportCleanupAssertions.IsRabbitManagedExchange(exchangeName, scenario))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        foreach (var queueName in queueNames)
        {
            await DeleteRabbitQueueAsync(queueName, cancellationToken);
        }

        foreach (var exchangeName in exchangeNames)
        {
            await DeleteRabbitExchangeAsync(exchangeName, cancellationToken);
        }
    }

    private async Task PrepareAzureScenarioAsync(TransportCleanupScenarioDefinition scenario, CancellationToken cancellationToken)
    {
        var client = CreateAzureAdministrationClient();
        var snapshot = await CaptureAzureSnapshotAsync(cancellationToken);
        var messageTopicName = TransportCleanupNaming.GetAzureTopicName(scenario.MessageContractType);
        var eventTopicName = TransportCleanupNaming.GetAzureTopicName(scenario.EventContractType);
        var responseQueueNames = snapshot.QueueNames
            .Where(queueName => TransportCleanupNaming.IsAzureResponseQueueName(queueName, scenario.ResponseContractType))
            .ToArray();

        if (snapshot.HasSubscription(messageTopicName, TransportCleanupNaming.GetAzurePermanentSubscriptionName(scenario.MessageContractType)))
        {
            await DeleteAzureSubscriptionAsync(client, messageTopicName, TransportCleanupNaming.GetAzurePermanentSubscriptionName(scenario.MessageContractType), cancellationToken);
        }

        if (snapshot.SubscriptionsByTopic.TryGetValue(eventTopicName, out var eventSubscriptions))
        {
            foreach (var subscriptionName in eventSubscriptions.Where(TransportCleanupNaming.IsAzureTemporarySubscriptionName))
            {
                await DeleteAzureSubscriptionAsync(client, eventTopicName, subscriptionName, cancellationToken);
            }
        }

        if (snapshot.HasSubscription(scenario.DirectTopicName, scenario.DirectSubscriptionName))
        {
            await DeleteAzureSubscriptionAsync(client, scenario.DirectTopicName, scenario.DirectSubscriptionName, cancellationToken);
        }

        foreach (var topicName in new[] { messageTopicName, eventTopicName, scenario.DirectTopicName }.Distinct(StringComparer.Ordinal))
        {
            if (snapshot.HasTopic(topicName))
            {
                await DeleteAzureTopicAsync(client, topicName, cancellationToken);
            }
        }

        foreach (var queueName in responseQueueNames
                     .Concat(new[] { TransportCleanupNaming.GetAzureRequestQueueName(scenario.RequestContractType), scenario.DirectQueueName })
                     .Distinct(StringComparer.Ordinal))
        {
            if (snapshot.HasQueue(queueName))
            {
                await DeleteAzureQueueAsync(client, queueName, cancellationToken);
            }
        }
    }

    private async Task CreateRabbitDirectEntitiesAsync(TransportCleanupScenarioDefinition scenario, CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _settings.RabbitMQ.Host,
            UserName = _settings.RabbitMQ.UserName,
            Password = _settings.RabbitMQ.Password,
            Port = _settings.RabbitMQ.Port,
            VirtualHost = _settings.RabbitMQ.VirtualHost
        };

        await using var connection = await factory.CreateConnectionAsync("transport-cleanup-integration", cancellationToken);
        await using var channel = await connection.CreateChannelAsync(options: null, cancellationToken);

        await channel.ExchangeDeclareAsync(
            exchange: scenario.DirectExchangeName,
            type: ExchangeType.Fanout,
            durable: true,
            autoDelete: false,
            arguments: null);

        await channel.QueueDeclareAsync(
            queue: scenario.DirectQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            queue: scenario.DirectQueueName,
            exchange: scenario.DirectExchangeName,
            routingKey: string.Empty,
            arguments: null,
            cancellationToken: cancellationToken);
    }

    private async Task CreateAzureDirectEntitiesAsync(TransportCleanupScenarioDefinition scenario, CancellationToken cancellationToken)
    {
        var client = CreateAzureAdministrationClient();

        if (!await client.QueueExistsAsync(scenario.DirectQueueName, cancellationToken))
        {
            await client.CreateQueueAsync(new CreateQueueOptions(scenario.DirectQueueName), cancellationToken);
        }

        if (!await client.TopicExistsAsync(scenario.DirectTopicName, cancellationToken))
        {
            await client.CreateTopicAsync(new CreateTopicOptions(scenario.DirectTopicName), cancellationToken);
        }

        if (!await client.SubscriptionExistsAsync(scenario.DirectTopicName, scenario.DirectSubscriptionName, cancellationToken))
        {
            await client.CreateSubscriptionAsync(
                new CreateSubscriptionOptions(scenario.DirectTopicName, scenario.DirectSubscriptionName),
                cancellationToken);
        }
    }

    private async Task<TransportEntitySnapshot> CaptureRabbitSnapshotAsync(CancellationToken cancellationToken)
    {
        var queueNames = await GetRabbitEntityNamesAsync($"api/queues/{GetEncodedRabbitVirtualHost()}?columns=name&disable_stats=true", cancellationToken);
        var exchangeNames = await GetRabbitEntityNamesAsync($"api/exchanges/{GetEncodedRabbitVirtualHost()}?columns=name&disable_stats=true", cancellationToken);

        return new TransportEntitySnapshot(
            QueueNames: queueNames,
            ExchangeNames: exchangeNames,
            TopicNames: Array.Empty<string>(),
            SubscriptionsByTopic: new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal));
    }

    private async Task<TransportEntitySnapshot> CaptureAzureSnapshotAsync(CancellationToken cancellationToken)
    {
        var client = CreateAzureAdministrationClient();
        var queueNames = new List<string>();
        var topicNames = new List<string>();
        var subscriptionsByTopic = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal);

        await foreach (var queue in client.GetQueuesAsync(cancellationToken))
        {
            queueNames.Add(queue.Name);
        }

        await foreach (var topic in client.GetTopicsAsync(cancellationToken))
        {
            topicNames.Add(topic.Name);

            var subscriptions = new List<string>();
            await foreach (var subscription in client.GetSubscriptionsAsync(topic.Name, cancellationToken))
            {
                subscriptions.Add(subscription.SubscriptionName);
            }

            subscriptionsByTopic[topic.Name] = subscriptions;
        }

        return new TransportEntitySnapshot(
            QueueNames: queueNames,
            ExchangeNames: Array.Empty<string>(),
            TopicNames: topicNames,
            SubscriptionsByTopic: subscriptionsByTopic);
    }

    private async Task<IReadOnlyCollection<string>> GetRabbitEntityNamesAsync(string relativeUrl, CancellationToken cancellationToken)
    {
        using var client = CreateRabbitManagementClient();
        using var response = await client.GetAsync(relativeUrl, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"RabbitMQ management API request to '{relativeUrl}' failed with status code {(int)response.StatusCode}.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var entities = await JsonSerializer.DeserializeAsync<List<RabbitManagementEntity>>(stream, JsonOptions, cancellationToken)
            ?? new List<RabbitManagementEntity>();

        return entities
            .Select(entity => entity.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private async Task DeleteRabbitQueueAsync(string queueName, CancellationToken cancellationToken)
    {
        using var client = CreateRabbitManagementClient();
        using var response = await client.DeleteAsync(
            $"api/queues/{GetEncodedRabbitVirtualHost()}/{Uri.EscapeDataString(queueName)}",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"RabbitMQ management API failed to delete queue '{queueName}' with status code {(int)response.StatusCode}.");
        }
    }

    private async Task DeleteRabbitExchangeAsync(string exchangeName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(exchangeName) || exchangeName.StartsWith("amq.", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        using var client = CreateRabbitManagementClient();
        using var response = await client.DeleteAsync(
            $"api/exchanges/{GetEncodedRabbitVirtualHost()}/{Uri.EscapeDataString(exchangeName)}",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"RabbitMQ management API failed to delete exchange '{exchangeName}' with status code {(int)response.StatusCode}.");
        }
    }

    private async Task DeleteAzureSubscriptionAsync(
        ServiceBusAdministrationClient client,
        string topicName,
        string subscriptionName,
        CancellationToken cancellationToken)
    {
        try
        {
            await client.DeleteSubscriptionAsync(topicName, subscriptionName, cancellationToken);
        }
        catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
        {
            logger.LogDebug(
                ex,
                "Azure Service Bus subscription {SubscriptionName} for topic {TopicName} was already removed while preparing the scenario.",
                subscriptionName,
                topicName);
        }
    }

    private async Task DeleteAzureTopicAsync(
        ServiceBusAdministrationClient client,
        string topicName,
        CancellationToken cancellationToken)
    {
        try
        {
            await client.DeleteTopicAsync(topicName, cancellationToken);
        }
        catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
        {
            logger.LogDebug(ex, "Azure Service Bus topic {TopicName} was already removed while preparing the scenario.", topicName);
        }
    }

    private async Task DeleteAzureQueueAsync(
        ServiceBusAdministrationClient client,
        string queueName,
        CancellationToken cancellationToken)
    {
        try
        {
            await client.DeleteQueueAsync(queueName, cancellationToken);
        }
        catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
        {
            logger.LogDebug(ex, "Azure Service Bus queue {QueueName} was already removed while preparing the scenario.", queueName);
        }
    }

    private HttpClient CreateRabbitManagementClient()
    {
        var client = new HttpClient
        {
            BaseAddress = BuildRabbitManagementBaseAddress()
        };

        var userName = string.IsNullOrWhiteSpace(_settings.RabbitMQ.ManagementUserName)
            ? _settings.RabbitMQ.UserName
            : _settings.RabbitMQ.ManagementUserName;
        var password = string.IsNullOrWhiteSpace(_settings.RabbitMQ.ManagementPassword)
            ? _settings.RabbitMQ.Password
            : _settings.RabbitMQ.ManagementPassword;
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{userName}:{password}"));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    private Uri BuildRabbitManagementBaseAddress()
    {
        if (!string.IsNullOrWhiteSpace(_settings.RabbitMQ.ManagementApiBaseAddress))
        {
            var url = _settings.RabbitMQ.ManagementApiBaseAddress!;
            return new Uri(url.EndsWith("/", StringComparison.Ordinal) ? url : url + "/", UriKind.Absolute);
        }

        return new UriBuilder(
            _settings.RabbitMQ.ManagementScheme,
            _settings.RabbitMQ.Host,
            _settings.RabbitMQ.ManagementPort)
        {
            Path = "/"
        }.Uri;
    }

    private string GetEncodedRabbitVirtualHost()
    {
        return Uri.EscapeDataString(_settings.RabbitMQ.VirtualHost);
    }

    private ServiceBusAdministrationClient CreateAzureAdministrationClient()
    {
        return new ServiceBusAdministrationClient(GetAzureAdministrationConnectionString());
    }

    private string GetAzureAdministrationConnectionString()
    {
        if (!string.IsNullOrWhiteSpace(_settings.AzureServiceBus.AdministrationConnectionString))
        {
            return ApplyAzureEndpointPortIfNeeded(
                _settings.AzureServiceBus.AdministrationConnectionString!,
                _settings.AzureServiceBus.EmulatorManagementPort,
                overwriteExplicitPort: false);
        }

        return ApplyAzureEndpointPortIfNeeded(
            _settings.AzureServiceBus.ConnectionString,
            _settings.AzureServiceBus.EmulatorManagementPort,
            overwriteExplicitPort: true);
    }

    private static string ApplyAzureEndpointPortIfNeeded(string connectionString, int port, bool overwriteExplicitPort)
    {
        if (!connectionString.Contains("UseDevelopmentEmulator=true", StringComparison.OrdinalIgnoreCase))
        {
            return connectionString;
        }

        var segments = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
        for (var index = 0; index < segments.Count; index++)
        {
            if (!segments[index].StartsWith("Endpoint=", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var endpointValue = segments[index]["Endpoint=".Length..];
            var endpointUri = new Uri(endpointValue);
            if (!overwriteExplicitPort && endpointUri.IsDefaultPort is false)
            {
                return connectionString;
            }

            var builder = new UriBuilder(endpointUri)
            {
                Port = port
            };

            segments[index] = "Endpoint=" + builder.Uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
            return string.Join(';', segments) + ";";
        }

        return connectionString;
    }

    private static IntegrationTestQsMessagingSettings BindSettings(IConfiguration configuration)
    {
        var settings = new IntegrationTestQsMessagingSettings();
        configuration.GetSection("QsMessaging").Bind(settings);
        return settings;
    }

    private sealed record RabbitManagementEntity(string Name);
}
