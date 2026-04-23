using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using QsMessaging.Public;
using QsMessaging.RabbitMq.Services.Interfaces;

namespace QsMessaging.RabbitMq.Services
{
    internal sealed class RqManagementService(
        ILogger<RqManagementService> logger,
        IQsMessagingConfiguration configuration) : IRqManagementService
    {
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public Task<IReadOnlyCollection<string>> GetQueueNamesAsync(CancellationToken cancellationToken = default)
        {
            return GetEntityNamesAsync($"api/queues/{GetEncodedVirtualHost()}?columns=name&disable_stats=true", cancellationToken);
        }

        public Task<IReadOnlyCollection<string>> GetExchangeNamesAsync(CancellationToken cancellationToken = default)
        {
            return GetEntityNamesAsync($"api/exchanges/{GetEncodedVirtualHost()}?columns=name&disable_stats=true", cancellationToken);
        }

        public Task DeleteQueueAsync(string queueName, CancellationToken cancellationToken = default)
        {
            return DeleteEntityAsync($"api/queues/{GetEncodedVirtualHost()}/{Uri.EscapeDataString(queueName)}", "queue", queueName, cancellationToken);
        }

        public Task DeleteExchangeAsync(string exchangeName, CancellationToken cancellationToken = default)
        {
            return DeleteEntityAsync($"api/exchanges/{GetEncodedVirtualHost()}/{Uri.EscapeDataString(exchangeName)}", "exchange", exchangeName, cancellationToken);
        }

        private async Task<IReadOnlyCollection<string>> GetEntityNamesAsync(string relativeUrl, CancellationToken cancellationToken)
        {
            using var client = CreateClient();
            using var response = await client.GetAsync(relativeUrl, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"RabbitMQ management API request to '{relativeUrl}' failed with status code {(int)response.StatusCode}.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var entities = await JsonSerializer.DeserializeAsync<List<RqManagementEntityDto>>(stream, _jsonOptions, cancellationToken)
                ?? new List<RqManagementEntityDto>();

            return entities
                .Select(entity => entity.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        private async Task DeleteEntityAsync(
            string relativeUrl,
            string entityType,
            string entityDisplayName,
            CancellationToken cancellationToken)
        {
            using var client = CreateClient();
            using var response = await client.DeleteAsync(relativeUrl, cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                logger.LogDebug("RabbitMQ {EntityType} {EntityDisplayName} was already removed.", entityType, entityDisplayName);
                return;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"RabbitMQ management API failed to delete {entityType} '{entityDisplayName}' with status code {(int)response.StatusCode}.");
            }

            logger.LogInformation("RabbitMQ {EntityType} {EntityDisplayName} deleted through management API.", entityType, entityDisplayName);
        }

        private HttpClient CreateClient()
        {
            var managementConfiguration = configuration.RabbitMQ;
            var client = new HttpClient
            {
                BaseAddress = BuildManagementBaseUri()
            };

            var userName = string.IsNullOrWhiteSpace(managementConfiguration.ManagementUserName)
                ? managementConfiguration.UserName
                : managementConfiguration.ManagementUserName;
            var password = string.IsNullOrWhiteSpace(managementConfiguration.ManagementPassword)
                ? managementConfiguration.Password
                : managementConfiguration.ManagementPassword;
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{userName}:{password}"));

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            return client;
        }

        private Uri BuildManagementBaseUri()
        {
            var managementConfiguration = configuration.RabbitMQ;
            if (!string.IsNullOrWhiteSpace(managementConfiguration.ManagementApiBaseAddress))
            {
                return new Uri(EnsureTrailingSlash(managementConfiguration.ManagementApiBaseAddress), UriKind.Absolute);
            }

            return new UriBuilder(
                managementConfiguration.ManagementScheme,
                managementConfiguration.Host,
                managementConfiguration.ManagementPort)
            {
                Path = "/"
            }.Uri;
        }

        private string GetEncodedVirtualHost()
        {
            return Uri.EscapeDataString(configuration.RabbitMQ.VirtualHost);
        }

        private static string EnsureTrailingSlash(string url)
        {
            return url.EndsWith("/", StringComparison.Ordinal) ? url : url + "/";
        }

        private sealed record RqManagementEntityDto(string Name);
    }
}
