using IntegrationTest.Common;

namespace CleanupAssertInstance01;

internal sealed class FullCleanUpTransportationScenario(
    ITransportCleanupTestAdministration administration,
    ITransportCleanupCheckpointStore checkpointStore,
    ILogger<FullCleanUpTransportationScenario> logger) : IScenario
{
    public async Task Run()
    {
        var scenario = TransportCleanupScenarioCatalog.FullCleanUpTransportation;

        try
        {
            await WaitForConditionAsync(
                scenario,
                snapshot => TransportCleanupAssertions.HasCreatedState(snapshot, administration.Transport, scenario),
                "created state");
            checkpointStore.MarkCreatedObserved(scenario);

            await WaitForConditionAsync(
                scenario,
                snapshot => TransportCleanupAssertions.HasExpectedStateAfterFullCleanUp(snapshot, administration.Transport, scenario),
                "full cleanup state");

            CollectionTestResults.PassTest(CleanupTestScenario.FullCleanUpTransportation);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Full cleanup assert scenario failed.");
            CollectionTestResults.FailTest(CleanupTestScenario.FullCleanUpTransportation);
        }
        finally
        {
            checkpointStore.MarkCreatedObserved(scenario);
            checkpointStore.MarkVerified(scenario);
        }
    }

    private async Task WaitForConditionAsync(
        TransportCleanupScenarioDefinition scenario,
        Func<TransportEntitySnapshot, bool> predicate,
        string stageName)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(60);

        while (DateTimeOffset.UtcNow < deadline)
        {
            var snapshot = await administration.CaptureAsync();
            if (predicate(snapshot))
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(300));
        }

        throw new TimeoutException($"Scenario '{scenario.ScenarioKey}' did not reach the expected {stageName} in time.");
    }
}
