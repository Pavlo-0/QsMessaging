namespace IntegrationTestV2.Contracts;

public static class ServiceIds
{
    public const string Runner = "runner";
    public const string Sender01 = "sender-01";
    public const string Sender02 = "sender-02";
    public const string Receiver01 = "receiver-01";
    public const string Receiver02 = "receiver-02";

    public static readonly string[] RequiredAgents =
    [
        Sender01,
        Sender02,
        Receiver01,
        Receiver02
    ];
}

public sealed record ServiceHeartbeatEvent(
    string ServiceId,
    string Role,
    DateTimeOffset SentUtc);

public sealed record SenderScenarioCommandEvent(
    Guid RunId,
    string ScenarioName,
    string TargetSenderId,
    int RequestCount,
    int MaxConcurrency,
    int BaseValue);

public sealed record ScaleRequest(
    Guid RequestId,
    string SenderId,
    int Number1,
    int Number2);

public sealed record ScaleResponse(
    Guid RequestId,
    string SenderId,
    string ReceiverId,
    int Sum);

public sealed record RequestObservation(
    Guid RequestId,
    string SenderId,
    string ReceiverId,
    int ExpectedSum,
    int ActualSum);

public sealed record SenderScenarioCompletedMessage(
    Guid RunId,
    string ScenarioName,
    string SenderId,
    bool IsSuccess,
    IReadOnlyList<RequestObservation> Observations,
    string? Error);
