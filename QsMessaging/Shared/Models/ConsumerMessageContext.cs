namespace QsMessaging.Shared.Models
{
    internal sealed record ConsumerMessageContext
    {
        public string TransportName { get; init; } = string.Empty;

        public string OriginalQueueName { get; init; } = string.Empty;

        public string? OriginalHashedQueueName { get; init; }

        public string? OriginalDestinationName { get; init; }

        public string? OriginalHashedDestinationName { get; init; }

        public string? RoutingKey { get; init; }

        public string? Subject { get; init; }

        public string? ReplyTo { get; init; }

        public string? CorrelationId { get; init; }

        public string? MessageId { get; init; }

        public string? ContentType { get; init; }

        public string? ContentEncoding { get; init; }

        public string? OriginalContractType { get; init; }

        public IReadOnlyDictionary<string, string?> Headers { get; init; }
            = new Dictionary<string, string?>();

        public IReadOnlyDictionary<string, string?> Metadata { get; init; }
            = new Dictionary<string, string?>();
    }
}
