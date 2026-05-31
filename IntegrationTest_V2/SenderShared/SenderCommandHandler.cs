using System.Collections.Concurrent;
using IntegrationTestV2.Common;
using IntegrationTestV2.Contracts;
using Microsoft.Extensions.Logging;
using QsMessaging.Public;
using QsMessaging.Public.Handler;

namespace IntegrationTestV2.Sender;

public sealed class SenderCommandHandler(
    ServiceIdentity identity,
    SenderCommandExecutor executor) : IQsEventHandler<SenderScenarioCommandEvent>
{
    public Task Consumer(SenderScenarioCommandEvent command)
    {
        return Consumer(command, CancellationToken.None);
    }

    public Task Consumer(SenderScenarioCommandEvent command, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(command.TargetSenderId, identity.ServiceId, StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        return executor.ExecuteAsync(command, cancellationToken);
    }
}

public sealed class SenderCommandExecutor(
    ServiceIdentity identity,
    IQsMessaging messaging,
    ILogger<SenderCommandExecutor> logger)
{
    public async Task ExecuteAsync(SenderScenarioCommandEvent command, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Starting {ScenarioName} ({RunId}) with {RequestCount} requests.",
            command.ScenarioName,
            command.RunId,
            command.RequestCount);

        var observations = new ConcurrentBag<RequestObservation>();
        string? error = null;

        try
        {
            using var concurrencyGate = new SemaphoreSlim(command.MaxConcurrency, command.MaxConcurrency);
            var requests = Enumerable.Range(0, command.RequestCount)
                .Select(index => ExecuteRequestAsync(command, index, concurrencyGate, observations, cancellationToken));

            await Task.WhenAll(requests);
        }
        catch (Exception ex)
        {
            error = ex.ToString();
            logger.LogError(ex, "Sender scenario {ScenarioName} ({RunId}) failed.", command.ScenarioName, command.RunId);
        }

        var result = new SenderScenarioCompletedMessage(
            command.RunId,
            command.ScenarioName,
            identity.ServiceId,
            error is null,
            observations.OrderBy(observation => observation.ExpectedSum).ToArray(),
            error);

        await messaging.SendMessageAsync(result);
    }

    private async Task ExecuteRequestAsync(
        SenderScenarioCommandEvent command,
        int index,
        SemaphoreSlim concurrencyGate,
        ConcurrentBag<RequestObservation> observations,
        CancellationToken cancellationToken)
    {
        await concurrencyGate.WaitAsync(cancellationToken);

        try
        {
            var request = new ScaleRequest(
                Guid.NewGuid(),
                identity.ServiceId,
                command.BaseValue + index,
                index + 1);
            var expectedSum = request.Number1 + request.Number2;
            var response = await messaging.RequestResponse<ScaleRequest, ScaleResponse>(request, cancellationToken);

            if (response.RequestId != request.RequestId ||
                !string.Equals(response.SenderId, identity.ServiceId, StringComparison.Ordinal) ||
                response.Sum != expectedSum)
            {
                throw new InvalidOperationException(
                    $"Invalid response for request {request.RequestId}: expected sender {identity.ServiceId} and sum {expectedSum}, " +
                    $"received sender {response.SenderId} and sum {response.Sum}.");
            }

            observations.Add(new RequestObservation(
                response.RequestId,
                response.SenderId,
                response.ReceiverId,
                expectedSum,
                response.Sum));
        }
        finally
        {
            concurrencyGate.Release();
        }
    }
}
