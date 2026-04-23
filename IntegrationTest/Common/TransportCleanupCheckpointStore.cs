using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IntegrationTest.Common;

internal interface ITransportCleanupCheckpointStore
{
    void ResetScenario(TransportCleanupScenarioDefinition scenario);

    void MarkCreatedObserved(TransportCleanupScenarioDefinition scenario);

    Task WaitForCreatedObservationAsync(
        TransportCleanupScenarioDefinition scenario,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);

    void MarkVerified(TransportCleanupScenarioDefinition scenario);

    Task WaitForVerificationAsync(
        TransportCleanupScenarioDefinition scenario,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}

internal sealed class FileTransportCleanupCheckpointStore(
    IConfiguration configuration,
    ILogger<FileTransportCleanupCheckpointStore> logger) : ITransportCleanupCheckpointStore
{
    private readonly string _baseDirectory = BuildBaseDirectory(configuration);

    public void ResetScenario(TransportCleanupScenarioDefinition scenario)
    {
        Directory.CreateDirectory(_baseDirectory);
        DeleteIfExists(GetCreatedObservedPath(scenario));
        DeleteIfExists(GetVerificationPath(scenario));
    }

    public void MarkCreatedObserved(TransportCleanupScenarioDefinition scenario)
    {
        Directory.CreateDirectory(_baseDirectory);
        File.WriteAllText(GetCreatedObservedPath(scenario), DateTimeOffset.UtcNow.ToString("O"));
    }

    public Task WaitForCreatedObservationAsync(
        TransportCleanupScenarioDefinition scenario,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        return WaitForMarkerAsync(
            scenario,
            GetCreatedObservedPath(scenario),
            "created observation",
            timeout,
            cancellationToken);
    }

    public void MarkVerified(TransportCleanupScenarioDefinition scenario)
    {
        Directory.CreateDirectory(_baseDirectory);
        File.WriteAllText(GetVerificationPath(scenario), DateTimeOffset.UtcNow.ToString("O"));
    }

    public async Task WaitForVerificationAsync(
        TransportCleanupScenarioDefinition scenario,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        await WaitForMarkerAsync(
            scenario,
            GetVerificationPath(scenario),
            "verification",
            timeout,
            cancellationToken);
    }

    private string GetCreatedObservedPath(TransportCleanupScenarioDefinition scenario)
    {
        return Path.Combine(_baseDirectory, $"{scenario.ScenarioKey}.created");
    }

    private string GetVerificationPath(TransportCleanupScenarioDefinition scenario)
    {
        return Path.Combine(_baseDirectory, $"{scenario.ScenarioKey}.verified");
    }

    private async Task WaitForMarkerAsync(
        TransportCleanupScenarioDefinition scenario,
        string markerPath,
        string markerDescription,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.Add(timeout);

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (File.Exists(markerPath))
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
        }

        logger.LogError("Timed out waiting for {MarkerDescription} of scenario {ScenarioKey}.", markerDescription, scenario.ScenarioKey);
        throw new TimeoutException($"{markerDescription} for scenario '{scenario.ScenarioKey}' was not completed in time.");
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static string BuildBaseDirectory(IConfiguration configuration)
    {
        var settings = new IntegrationTestQsMessagingSettings();
        configuration.GetSection("QsMessaging").Bind(settings);

        return Path.Combine(
            Path.GetTempPath(),
            "QsMessaging",
            "TransportCleanupIntegration",
            settings.Transport.ToString());
    }
}
