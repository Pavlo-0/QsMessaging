using System.Text;

namespace QsMessaging.AzureServiceBus.Services
{
    internal static class ConnectionStringHelper
    {
        public static string GetAdministrationConnectionString(QsAzureServiceBusConfiguration configuration)
        {
            if (!string.IsNullOrWhiteSpace(configuration.AdministrationConnectionString))
            {
                return configuration.AdministrationConnectionString;
            }

            if (string.IsNullOrWhiteSpace(configuration.ConnectionString))
            {
                return configuration.ConnectionString;
            }

            var sections = Parse(configuration.ConnectionString);
            if (!sections.TryGetValue("UseDevelopmentEmulator", out var useDevelopmentEmulator) ||
                !bool.TryParse(useDevelopmentEmulator, out var isDevelopmentEmulator) ||
                !isDevelopmentEmulator)
            {
                return configuration.ConnectionString;
            }

            if (!sections.TryGetValue("Endpoint", out var endpointRaw) ||
                !Uri.TryCreate(endpointRaw, UriKind.Absolute, out var endpoint) ||
                !endpoint.IsDefaultPort)
            {
                return configuration.ConnectionString;
            }

            var builder = new UriBuilder(endpoint)
            {
                Port = configuration.EmulatorManagementPort
            };

            sections["Endpoint"] = builder.Uri.GetLeftPart(UriPartial.Authority);

            return Serialize(sections);
        }

        internal static Dictionary<string, string> Parse(string connectionString)
        {
            var sections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var segment in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var separatorIndex = segment.IndexOf('=');
                if (separatorIndex <= 0)
                {
                    continue;
                }

                var key = segment[..separatorIndex];
                var value = separatorIndex == segment.Length - 1 ? string.Empty : segment[(separatorIndex + 1)..];
                sections[key] = value;
            }

            return sections;
        }

        private static string Serialize(IReadOnlyDictionary<string, string> sections)
        {
            var builder = new StringBuilder();

            foreach (var section in sections)
            {
                builder.Append(section.Key)
                    .Append('=')
                    .Append(section.Value)
                    .Append(';');
            }

            return builder.ToString();
        }
    }
}
