using QsMessaging.Public;
using System.Text.Json;

namespace QsMessaging.Shared
{
    internal static class SerializationMetadata
    {
        public const string ContractVersionHeader = "qs-contract-version";
        public const string ContractTypeHeader = "qs-contract-type";
        public const string ContentEncodingHeader = "content-encoding";

        public static JsonSerializerOptions GetJsonSerializerOptions(IQsMessagingConfiguration configuration)
        {
            return configuration.Serialization?.JsonSerializerOptions ?? new JsonSerializerOptions();
        }

        public static string GetContentType(IQsMessagingConfiguration configuration)
        {
            return UseConfiguredOrDefault(configuration.Serialization?.ContentType, "application/json");
        }

        public static string GetContentEncoding(IQsMessagingConfiguration configuration)
        {
            return UseConfiguredOrDefault(configuration.Serialization?.ContentEncoding, "utf-8");
        }

        public static string GetContractVersion(IQsMessagingConfiguration configuration)
        {
            return UseConfiguredOrDefault(configuration.Serialization?.ContractVersion, "1");
        }

        private static string UseConfiguredOrDefault(string? value, string defaultValue)
        {
            return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
        }
    }
}
