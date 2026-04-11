using QsMessaging.RabbitMq;
using System.Text;

namespace QsMessaging.AzureServiceBus.Services
{
    internal static class ServiceBusEntityNameFormatter
    {
        private const int QueueOrTopicMaxLength = 260;
        private const int SubscriptionMaxLength = 50;
        private const int HashLength = 16;

        public static string FormatQueueName(string queueName)
        {
            return Format(queueName, QueueOrTopicMaxLength, "queue");
        }

        public static string FormatTopicName(string topicName)
        {
            return Format(topicName, QueueOrTopicMaxLength, "topic");
        }

        public static string FormatEntityPath(string entityPath)
        {
            return Format(entityPath, QueueOrTopicMaxLength, "entity");
        }

        public static string FormatSubscriptionName(string subscriptionName)
        {
            return Format(subscriptionName, SubscriptionMaxLength, "sub");
        }

        private static string Format(string rawName, int maxLength, string fallbackPrefix)
        {
            if (string.IsNullOrWhiteSpace(rawName))
            {
                throw new ArgumentException("Entity name cannot be null or empty.", nameof(rawName));
            }

            if (maxLength <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxLength));
            }

            if (IsSafe(rawName, maxLength))
            {
                return rawName;
            }

            var sanitized = Sanitize(rawName, fallbackPrefix);
            var hash = NameGenerator.HashString(rawName, HashLength);
            var suffix = "." + hash;
            var baseMaxLength = maxLength - suffix.Length;

            if (baseMaxLength <= 0)
            {
                return hash[..Math.Min(hash.Length, maxLength)];
            }

            if (sanitized.Length > baseMaxLength)
            {
                sanitized = sanitized[..baseMaxLength];
                sanitized = TrimToAlphaNumericEdges(sanitized);
            }

            if (string.IsNullOrEmpty(sanitized))
            {
                sanitized = fallbackPrefix.Length > baseMaxLength
                    ? fallbackPrefix[..baseMaxLength]
                    : fallbackPrefix;
                sanitized = TrimToAlphaNumericEdges(sanitized);
            }

            if (string.IsNullOrEmpty(sanitized))
            {
                return hash[..Math.Min(hash.Length, maxLength)];
            }

            return sanitized + suffix;
        }

        private static string Sanitize(string rawName, string fallbackPrefix)
        {
            var builder = new StringBuilder(rawName.Length);

            foreach (var symbol in rawName)
            {
                builder.Append(NormalizeCharacter(symbol));
            }

            var sanitized = TrimToAlphaNumericEdges(builder.ToString());
            return string.IsNullOrEmpty(sanitized) ? fallbackPrefix : sanitized;
        }

        private static char NormalizeCharacter(char symbol)
        {
            if (IsAllowed(symbol))
            {
                return symbol;
            }

            return symbol switch
            {
                ':' or '+' or '/' or '\\' => '.',
                _ => '_'
            };
        }

        private static string TrimToAlphaNumericEdges(string value)
        {
            var start = 0;
            var end = value.Length - 1;

            while (start <= end && !IsAsciiLetterOrDigit(value[start]))
            {
                start++;
            }

            while (end >= start && !IsAsciiLetterOrDigit(value[end]))
            {
                end--;
            }

            return start > end ? string.Empty : value[start..(end + 1)];
        }

        private static bool IsSafe(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length > maxLength)
            {
                return false;
            }

            if (!IsAsciiLetterOrDigit(value[0]) || !IsAsciiLetterOrDigit(value[^1]))
            {
                return false;
            }

            return value.All(IsAllowed);
        }

        private static bool IsAllowed(char symbol)
        {
            return IsAsciiLetterOrDigit(symbol) || symbol is '.' or '-' or '_';
        }

        private static bool IsAsciiLetterOrDigit(char symbol)
        {
            return (symbol >= 'a' && symbol <= 'z')
                || (symbol >= 'A' && symbol <= 'Z')
                || (symbol >= '0' && symbol <= '9');
        }
    }
}
