using IntegrationTestV2.Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QsMessaging.Public;

namespace IntegrationTestV2.Runner;

public sealed class IntegrationSuiteWorker(
    IQsMessaging messaging,
    SuiteState state,
    Dashboard dashboard,
    SenderResultInbox senderResultInbox,
    IssueLog issueLog,
    RunnerOptions options,
    IHostApplicationLifetime applicationLifetime,
    ILogger<IntegrationSuiteWorker> logger) : BackgroundService
{
    private static readonly TimeSpan ServiceHeartbeatMaxAge = TimeSpan.FromSeconds(5);
    private readonly TimeSpan _scenarioTimeout = TimeSpan.FromSeconds(options.ScenarioTimeoutSeconds);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        dashboard.Render();

        var ready = await RunScenarioAsync(
            state.AllScenarioNames[0],
            "Waiting for sender-01, sender-02, receiver-01 and receiver-02 heartbeats",
            WaitForAgentsAsync,
            stoppingToken);

        if (ready)
        {
            await RunScenarioAsync(
                state.AllScenarioNames[1],
                "Sending one request directly from runner",
                RunOrdinaryRunnerRequestAsync,
                stoppingToken);
            await RunScenarioAsync(
                state.AllScenarioNames[2],
                "Sending 10 sequential requests from sender-01",
                RunSingleSenderSequentialAsync,
                stoppingToken);
            await RunScenarioAsync(
                state.AllScenarioNames[3],
                "Sending 20 concurrent requests from sender-01",
                RunSingleSenderConcurrentAsync,
                stoppingToken);
            await RunScenarioAsync(
                state.AllScenarioNames[4],
                "Sending 60 requests concurrently from two sender instances",
                RunTwoSenderScaleOutAsync,
                stoppingToken);
            await RunScenarioAsync(
                state.AllScenarioNames[5],
                "Combining 20 runner requests with 40 requests from two sender instances",
                RunMixedAsync,
                stoppingToken);
        }
        else
        {
            state.SkipWaiting("Skipped because required agents are offline");
        }

        dashboard.Render(isFinal: true);
        Environment.ExitCode = state.HasFailures ? 1 : 0;

        if (options.ExitAfterRun)
        {
            applicationLifetime.StopApplication();
            return;
        }

        Console.WriteLine(" Test run finished. Press Ctrl+C to stop the runner.");
    }

    private async Task<bool> RunScenarioAsync(
        string name,
        string details,
        Func<CancellationToken, Task<string>> scenario,
        CancellationToken cancellationToken)
    {
        state.Start(name, details);
        dashboard.Render();

        try
        {
            var resultDetails = await scenario(cancellationToken);
            state.Pass(name, resultDetails);
            dashboard.Render();
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Integration scenario {ScenarioName} failed.", name);
            issueLog.Write(name, ex);
            state.Fail(name, ex.Message);
            dashboard.Render();
            return false;
        }
    }

    private async Task<string> WaitForAgentsAsync(CancellationToken cancellationToken)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddSeconds(options.AgentReadyTimeoutSeconds);

        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            if (state.HaveLiveServices(ServiceIds.RequiredAgents, ServiceHeartbeatMaxAge))
            {
                return "All required service instances are online";
            }

            if (dashboard.IsInteractive)
            {
                dashboard.Render();
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }

        var missingServices = state.GetMissingServices(ServiceIds.RequiredAgents, ServiceHeartbeatMaxAge);
        throw new TimeoutException($"Timed out waiting for agents: {string.Join(", ", missingServices)}.");
    }

    private async Task<string> RunOrdinaryRunnerRequestAsync(CancellationToken cancellationToken)
    {
        var observation = await SendRunnerRequestAsync(0, 10, cancellationToken);
        return $"Receiver {observation.ReceiverId} returned the expected sum {observation.ActualSum}";
    }

    private async Task<string> RunSingleSenderSequentialAsync(CancellationToken cancellationToken)
    {
        var results = await RunSenderCommandsAsync(
            state.AllScenarioNames[2],
            [new SenderCommand(ServiceIds.Sender01, 10, 1, 100)],
            cancellationToken);

        var observations = GetValidatedObservations(results, 10);
        return $"{observations.Count} responses validated from {ServiceIds.Sender01}";
    }

    private async Task<string> RunSingleSenderConcurrentAsync(CancellationToken cancellationToken)
    {
        var results = await RunSenderCommandsAsync(
            state.AllScenarioNames[3],
            [new SenderCommand(ServiceIds.Sender01, 20, 10, 1_000)],
            cancellationToken);

        var observations = GetValidatedObservations(results, 20);
        return $"{observations.Count} concurrent responses validated from {ServiceIds.Sender01}";
    }

    private async Task<string> RunTwoSenderScaleOutAsync(CancellationToken cancellationToken)
    {
        var results = await RunSenderCommandsAsync(
            state.AllScenarioNames[4],
            [
                new SenderCommand(ServiceIds.Sender01, 30, 10, 10_000),
                new SenderCommand(ServiceIds.Sender02, 30, 10, 20_000)
            ],
            cancellationToken);

        var observations = GetValidatedObservations(results, 60);
        ValidateSenders(observations, ServiceIds.Sender01, ServiceIds.Sender02);
        ValidateReceivers(observations);
        return "Both senders completed requests and both receivers processed responses";
    }

    private async Task<string> RunMixedAsync(CancellationToken cancellationToken)
    {
        var senderResultsTask = RunSenderCommandsAsync(
            state.AllScenarioNames[5],
            [
                new SenderCommand(ServiceIds.Sender01, 20, 10, 30_000),
                new SenderCommand(ServiceIds.Sender02, 20, 10, 40_000)
            ],
            cancellationToken);
        var runnerRequestsTask = Task.WhenAll(
            Enumerable.Range(0, 20)
                .Select(index => SendRunnerRequestAsync(index, 50_000, cancellationToken)));

        await Task.WhenAll(senderResultsTask, runnerRequestsTask);

        var senderObservations = GetValidatedObservations(await senderResultsTask, 40);
        var observations = senderObservations
            .Concat(await runnerRequestsTask)
            .ToArray();

        ValidateSenders(observations, ServiceIds.Runner, ServiceIds.Sender01, ServiceIds.Sender02);
        ValidateReceivers(observations);
        return "Runner and both senders received valid responses from both receivers";
    }

    private async Task<IReadOnlyList<SenderScenarioCompletedMessage>> RunSenderCommandsAsync(
        string scenarioName,
        IReadOnlyList<SenderCommand> commands,
        CancellationToken cancellationToken)
    {
        var runId = Guid.NewGuid();

        foreach (var command in commands)
        {
            senderResultInbox.Prepare(runId, command.SenderId);
        }

        foreach (var command in commands)
        {
            await messaging.SendEventAsync(new SenderScenarioCommandEvent(
                runId,
                scenarioName,
                command.SenderId,
                command.RequestCount,
                command.MaxConcurrency,
                command.BaseValue));
        }

        return await Task.WhenAll(
            commands.Select(command =>
                senderResultInbox.WaitAsync(runId, command.SenderId, _scenarioTimeout, cancellationToken)));
    }

    private async Task<RequestObservation> SendRunnerRequestAsync(
        int index,
        int baseValue,
        CancellationToken cancellationToken)
    {
        var request = new ScaleRequest(
            Guid.NewGuid(),
            ServiceIds.Runner,
            baseValue + index,
            index + 1);
        var expectedSum = request.Number1 + request.Number2;
        var response = await messaging.RequestResponse<ScaleRequest, ScaleResponse>(request, cancellationToken);

        if (response.RequestId != request.RequestId ||
            !string.Equals(response.SenderId, ServiceIds.Runner, StringComparison.Ordinal) ||
            response.Sum != expectedSum)
        {
            throw new InvalidOperationException(
                $"Invalid response for runner request {request.RequestId}: expected sum {expectedSum}, received {response.Sum}.");
        }

        return new RequestObservation(
            response.RequestId,
            response.SenderId,
            response.ReceiverId,
            expectedSum,
            response.Sum);
    }

    private static IReadOnlyList<RequestObservation> GetValidatedObservations(
        IEnumerable<SenderScenarioCompletedMessage> results,
        int expectedObservationCount)
    {
        var materializedResults = results.ToArray();
        var failedResult = materializedResults.FirstOrDefault(result => !result.IsSuccess);
        if (failedResult is not null)
        {
            throw new InvalidOperationException(
                $"Sender {failedResult.SenderId} failed scenario {failedResult.ScenarioName}: {failedResult.Error}");
        }

        var observations = materializedResults.SelectMany(result => result.Observations).ToArray();
        if (observations.Length != expectedObservationCount)
        {
            throw new InvalidOperationException(
                $"Expected {expectedObservationCount} observations, received {observations.Length}.");
        }

        return observations;
    }

    private static void ValidateSenders(IEnumerable<RequestObservation> observations, params string[] expectedSenders)
    {
        var actualSenders = observations
            .Select(observation => observation.SenderId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(senderId => senderId)
            .ToArray();
        var missingSenders = expectedSenders.Except(actualSenders, StringComparer.Ordinal).ToArray();

        if (missingSenders.Length > 0)
        {
            throw new InvalidOperationException($"No responses were observed for senders: {string.Join(", ", missingSenders)}.");
        }
    }

    private static void ValidateReceivers(IEnumerable<RequestObservation> observations)
    {
        var actualReceivers = observations
            .Select(observation => observation.ReceiverId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(receiverId => receiverId)
            .ToArray();
        var missingReceivers = new[] { ServiceIds.Receiver01, ServiceIds.Receiver02 }
            .Except(actualReceivers, StringComparer.Ordinal)
            .ToArray();

        if (missingReceivers.Length > 0)
        {
            throw new InvalidOperationException(
                $"Horizontal receiver distribution was not observed. Missing receivers: {string.Join(", ", missingReceivers)}.");
        }
    }

    private sealed record SenderCommand(
        string SenderId,
        int RequestCount,
        int MaxConcurrency,
        int BaseValue);
}
