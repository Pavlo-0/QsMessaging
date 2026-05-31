namespace IntegrationTestV2.Runner;

public sealed class RunnerOptions
{
    public int AgentReadyTimeoutSeconds { get; set; } = 20;

    public int ScenarioTimeoutSeconds { get; set; } = 30;

    public bool ExitAfterRun { get; set; }

    public string LogDirectory { get; set; } = "../logs";
}
