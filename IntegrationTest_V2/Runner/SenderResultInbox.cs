using System.Collections.Concurrent;
using IntegrationTestV2.Contracts;

namespace IntegrationTestV2.Runner;

public sealed class SenderResultInbox
{
    private readonly ConcurrentDictionary<(Guid RunId, string SenderId), TaskCompletionSource<SenderScenarioCompletedMessage>> _waiters = new();

    public void Prepare(Guid runId, string senderId)
    {
        var waiter = new TaskCompletionSource<SenderScenarioCompletedMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_waiters.TryAdd((runId, senderId), waiter))
        {
            throw new InvalidOperationException($"A result waiter already exists for run {runId} and sender {senderId}.");
        }
    }

    public void Complete(SenderScenarioCompletedMessage result)
    {
        if (_waiters.TryGetValue((result.RunId, result.SenderId), out var waiter))
        {
            waiter.TrySetResult(result);
        }
    }

    public async Task<SenderScenarioCompletedMessage> WaitAsync(
        Guid runId,
        string senderId,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (!_waiters.TryGetValue((runId, senderId), out var waiter))
        {
            throw new InvalidOperationException($"No result waiter exists for run {runId} and sender {senderId}.");
        }

        try
        {
            return await waiter.Task.WaitAsync(timeout, cancellationToken);
        }
        finally
        {
            _waiters.TryRemove((runId, senderId), out _);
        }
    }
}
