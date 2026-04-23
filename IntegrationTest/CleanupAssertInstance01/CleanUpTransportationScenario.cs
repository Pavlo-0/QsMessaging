using IntegrationTest.Common;
using QsMessaging.Public;

namespace CleanupAssertInstance01;

internal sealed class CleanUpTransportationScenario(
    ITransportCleanupTestAdministration administration,
    ITransportCleanupCheckpointStore checkpointStore,
    ILogger<CleanUpTransportationScenario> logger) : IScenario
{
    public async Task Run()
    {
        var scenario = TransportCleanupScenarioCatalog.CleanUpTransportation;

        try
        {
            await WaitForConditionAsync(
                scenario,
                snapshot => TransportCleanupAssertions.HasCreatedState(snapshot, administration.Transport, scenario),
                "created state");
            checkpointStore.MarkCreatedObserved(scenario);

            await WaitForConditionAsync(
                scenario,
                snapshot => TransportCleanupAssertions.HasExpectedStateAfterCleanUp(snapshot, administration.Transport, scenario),
                "cleanup state");

            CollectionTestResults.PassTest(CleanupTestScenario.CleanUpTransportation);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Cleanup assert scenario failed.");
            CollectionTestResults.FailTest(CleanupTestScenario.CleanUpTransportation);
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
