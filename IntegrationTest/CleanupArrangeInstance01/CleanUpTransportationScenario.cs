using IntegrationTest.Common;
using QsMessaging.Public;
using TestContract.TransportCleanup;

namespace CleanupArrangeInstance01;

internal sealed class CleanUpTransportationScenario(
    IQsMessaging messaging,
    ITransportCleanupExecutor cleanupExecutor,
    ITransportCleanupTestAdministration administration,
    ITransportCleanupCheckpointStore checkpointStore) : IScenario
{
    public async Task Run()
    {
        var scenario = TransportCleanupScenarioCatalog.CleanUpTransportation;
        checkpointStore.ResetScenario(scenario);

        await cleanupExecutor.EnsureOpenAsync();
        await administration.CreateDirectEntitiesAsync(scenario);

        var id = Guid.NewGuid().ToString("N");
        await messaging.SendMessageAsync(new CleanUpTransportMessageContract(id));
        await messaging.SendEventAsync(new CleanUpTransportEventContract(id));

        var response = await messaging.RequestResponse<CleanUpTransportRequestContract, CleanUpTransportResponseContract>(
            new CleanUpTransportRequestContract(id));

        if (response.Id != id || response.Status != "cleaned")
        {
            throw new InvalidOperationException("The cleanup arrange scenario did not receive the expected response.");
        }

        await checkpointStore.WaitForCreatedObservationAsync(scenario, TimeSpan.FromSeconds(60));
        await cleanupExecutor.CleanUpAsync();
        await checkpointStore.WaitForVerificationAsync(scenario, TimeSpan.FromSeconds(60));
    }
}
