using IntegrationTest.Common;
using QsMessaging.Public;
using TestContract.TransportCleanup;

namespace CleanupArrangeInstance01;

internal sealed class FullCleanUpTransportationScenario(
    IQsMessaging messaging,
    ITransportCleanupExecutor cleanupExecutor,
    ITransportCleanupTestAdministration administration,
    ITransportCleanupCheckpointStore checkpointStore) : IScenario
{
    public async Task Run()
    {
        var scenario = TransportCleanupScenarioCatalog.FullCleanUpTransportation;
        checkpointStore.ResetScenario(scenario);

        await cleanupExecutor.EnsureOpenAsync();
        await administration.CreateDirectEntitiesAsync(scenario);

        var id = Guid.NewGuid().ToString("N");
        await messaging.SendMessageAsync(new FullCleanUpTransportMessageContract(id));
        await messaging.SendEventAsync(new FullCleanUpTransportEventContract(id));

        var response = await messaging.RequestResponse<FullCleanUpTransportRequestContract, FullCleanUpTransportResponseContract>(
            new FullCleanUpTransportRequestContract(id));

        if (response.Id != id || response.Status != "full-cleaned")
        {
            throw new InvalidOperationException("The full cleanup arrange scenario did not receive the expected response.");
        }

        await checkpointStore.WaitForCreatedObservationAsync(scenario, TimeSpan.FromSeconds(60));
        await cleanupExecutor.FullCleanUpAsync();
        await checkpointStore.WaitForVerificationAsync(scenario, TimeSpan.FromSeconds(60));
    }
}
