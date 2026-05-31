using IntegrationTestV2.Contracts;

namespace IntegrationTestV2.Runner;

public enum ScenarioStatus
{
    Waiting,
    Progress,
    Passed,
    Failed,
    Skipped
}

public sealed record ScenarioSnapshot(
    string Name,
    ScenarioStatus Status,
    string Details,
    TimeSpan? Duration);

public sealed record ServiceSnapshot(
    string ServiceId,
    string Role,
    bool IsOnline,
    DateTimeOffset LastSeenUtc);

public sealed class SuiteState
{
    private static readonly string[] ScenarioNames =
    [
        "Agents ready: 2 senders + 2 receivers",
        "Ordinary request-response: runner -> receiver",
        "Ordinary request-response: sender-01 sequential",
        "Ordinary request-response: sender-01 concurrent",
        "Scale-out request-response: 2 senders -> 2 receivers",
        "Mixed request-response: runner + 2 senders -> 2 receivers"
    ];

    private readonly object _sync = new();
    private readonly Dictionary<string, MutableScenario> _scenarios =
        ScenarioNames.ToDictionary(name => name, name => new MutableScenario(name));
    private readonly Dictionary<string, ServiceSnapshot> _services = new();

    public IReadOnlyList<string> AllScenarioNames => ScenarioNames;

    public void RecordHeartbeat(ServiceHeartbeatEvent heartbeat)
    {
        lock (_sync)
        {
            _services[heartbeat.ServiceId] = new ServiceSnapshot(
                heartbeat.ServiceId,
                heartbeat.Role,
                true,
                heartbeat.SentUtc);
        }
    }

    public bool HaveLiveServices(IEnumerable<string> requiredServiceIds, TimeSpan maxAge)
    {
        var minHeartbeatUtc = DateTimeOffset.UtcNow - maxAge;

        lock (_sync)
        {
            return requiredServiceIds.All(serviceId =>
                _services.TryGetValue(serviceId, out var service) &&
                service.LastSeenUtc >= minHeartbeatUtc);
        }
    }

    public IReadOnlyList<string> GetMissingServices(IEnumerable<string> requiredServiceIds, TimeSpan maxAge)
    {
        var minHeartbeatUtc = DateTimeOffset.UtcNow - maxAge;

        lock (_sync)
        {
            return requiredServiceIds
                .Where(serviceId =>
                    !_services.TryGetValue(serviceId, out var service) ||
                    service.LastSeenUtc < minHeartbeatUtc)
                .ToArray();
        }
    }

    public IReadOnlyList<ServiceSnapshot> GetServices(TimeSpan maxAge)
    {
        var minHeartbeatUtc = DateTimeOffset.UtcNow - maxAge;

        lock (_sync)
        {
            return ServiceIds.RequiredAgents
                .Select(serviceId =>
                {
                    if (_services.TryGetValue(serviceId, out var service))
                    {
                        return service with { IsOnline = service.LastSeenUtc >= minHeartbeatUtc };
                    }

                    return new ServiceSnapshot(serviceId, GetRole(serviceId), false, DateTimeOffset.MinValue);
                })
                .ToArray();
        }
    }

    public void Start(string name, string details)
    {
        lock (_sync)
        {
            var scenario = _scenarios[name];
            scenario.Status = ScenarioStatus.Progress;
            scenario.Details = details;
            scenario.StartedUtc = DateTimeOffset.UtcNow;
            scenario.Duration = null;
        }
    }

    public void Pass(string name, string details)
    {
        Complete(name, ScenarioStatus.Passed, details);
    }

    public void Fail(string name, string details)
    {
        Complete(name, ScenarioStatus.Failed, details);
    }

    public void SkipWaiting(string details)
    {
        lock (_sync)
        {
            foreach (var scenario in _scenarios.Values.Where(scenario => scenario.Status == ScenarioStatus.Waiting))
            {
                scenario.Status = ScenarioStatus.Skipped;
                scenario.Details = details;
            }
        }
    }

    public bool HasFailures
    {
        get
        {
            lock (_sync)
            {
                return _scenarios.Values.Any(scenario => scenario.Status == ScenarioStatus.Failed);
            }
        }
    }

    public IReadOnlyList<ScenarioSnapshot> GetScenarios()
    {
        lock (_sync)
        {
            return _scenarios.Values
                .Select(scenario => new ScenarioSnapshot(
                    scenario.Name,
                    scenario.Status,
                    scenario.Details,
                    scenario.Duration))
                .ToArray();
        }
    }

    private void Complete(string name, ScenarioStatus status, string details)
    {
        lock (_sync)
        {
            var scenario = _scenarios[name];
            scenario.Status = status;
            scenario.Details = details;
            scenario.Duration = scenario.StartedUtc is null
                ? null
                : DateTimeOffset.UtcNow - scenario.StartedUtc.Value;
        }
    }

    private static string GetRole(string serviceId)
    {
        return serviceId.StartsWith("sender", StringComparison.Ordinal) ? "sender" : "receiver";
    }

    private sealed class MutableScenario(string name)
    {
        public string Name { get; } = name;

        public ScenarioStatus Status { get; set; } = ScenarioStatus.Waiting;

        public string Details { get; set; } = "Queued";

        public DateTimeOffset? StartedUtc { get; set; }

        public TimeSpan? Duration { get; set; }
    }
}
