namespace QsMessaging.Public.Handler
{
    /// <summary>
    /// Diagnostic envelope created when QsMessaging cannot successfully process a received message.
    /// </summary>
    public sealed class FailedMessageWrapper
    {
        public Guid FailedEnvelopeId { get; init; } = Guid.NewGuid();

        public string TransportName { get; init; } = string.Empty;

        public string OriginalQueueName { get; init; } = string.Empty;

        public string? OriginalHashedQueueName { get; init; }

        public string ErrorQueueName { get; init; } = string.Empty;

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

        public string? HandlerType { get; init; }

        public string ErrorConsumerType { get; init; } = string.Empty;

        public string? ErrorReason { get; init; }

        public object? OriginalMessagePayload { get; init; }

        public byte[]? OriginalMessageBody { get; init; }

        public string? OriginalMessageBodyText { get; init; }

        public IReadOnlyDictionary<string, string?> OriginalMessageHeaders { get; init; }
            = new Dictionary<string, string?>();

        public int HandlerAttempts { get; init; }

        public int ConfiguredMaxRetryAttempts { get; init; }

        public IReadOnlyList<FailedMessageError> Errors { get; init; }
            = Array.Empty<FailedMessageError>();

        public DateTimeOffset CreatedUtc { get; init; } = DateTimeOffset.UtcNow;

        public DateTimeOffset? SentToErrorQueueUtc { get; set; }

        public IReadOnlyDictionary<string, string?> Metadata { get; init; }
            = new Dictionary<string, string?>();
    }

    public sealed class FailedMessageError
    {
        public DateTimeOffset OccurredUtc { get; init; } = DateTimeOffset.UtcNow;

        public string ExceptionType { get; init; } = string.Empty;

        public string Message { get; init; } = string.Empty;

        public string? StackTrace { get; init; }

        public FailedMessageError? InnerException { get; init; }
    }
}
