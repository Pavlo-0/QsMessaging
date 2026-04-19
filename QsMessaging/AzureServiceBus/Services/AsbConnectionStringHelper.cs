using System.Text;

namespace QsMessaging.AzureServiceBus.Services
{
    internal static class AsbConnectionStringHelper
    {
        public static string GetClientConnectionString(QsAzureServiceBusConfiguration configuration)
        {
            return ApplyEmulatorPortIfNeeded(configuration.ConnectionString, configuration.EmulatorAmqpPort, overwriteExplicitPort: false);
        }

        public static string GetAdministrationConnectionString(QsAzureServiceBusConfiguration configuration)
        {
            if (!string.IsNullOrWhiteSpace(configuration.AdministrationConnectionString))
            {
                return ApplyEmulatorPortIfNeeded(configuration.AdministrationConnectionString, configuration.EmulatorManagementPort, overwriteExplicitPort: false);
            }

            return ApplyEmulatorPortIfNeeded(configuration.ConnectionString, configuration.EmulatorManagementPort, overwriteExplicitPort: true);
        }

        private static string ApplyEmulatorPortIfNeeded(string connectionString, int emulatorPort, bool overwriteExplicitPort)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return connectionString;
            }

            var sections = Parse(connectionString);
            if (!sections.TryGetValue("UseDevelopmentEmulator", out var useDevelopmentEmulator) ||
                !bool.TryParse(useDevelopmentEmulator, out var isDevelopmentEmulator) ||
                !isDevelopmentEmulator)
            {
                return connectionString;
            }

            if (!sections.TryGetValue("Endpoint", out var endpointRaw) ||
                !Uri.TryCreate(endpointRaw, UriKind.Absolute, out var endpoint))
            {
                return connectionString;
            }

            if (!overwriteExplicitPort && !endpoint.IsDefaultPort)
            {
                return connectionString;
            }

            var builder = new UriBuilder(endpoint)
            {
                Port = emulatorPort
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
