namespace AssertInstance01
{
    internal interface IScenarioExecutionGate
    {
        IAsyncDisposable BeginBlock();

        Task WaitUntilReadyAsync(CancellationToken cancellationToken = default);
    }
}
