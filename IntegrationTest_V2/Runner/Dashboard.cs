namespace IntegrationTestV2.Runner;

public sealed class Dashboard(SuiteState state, IssueLog issueLog)
{
    private static readonly TimeSpan ServiceHeartbeatMaxAge = TimeSpan.FromSeconds(5);
    private readonly object _sync = new();

    public bool IsInteractive => !Console.IsOutputRedirected;

    public void Render(bool isFinal = false)
    {
        lock (_sync)
        {
            if (IsInteractive)
            {
                TryClearConsole();
            }

            Console.WriteLine("==============================================================");
            Console.WriteLine(" QsMessaging IntegrationTest_V2 | RabbitMQ request-response");
            Console.WriteLine("==============================================================");
            Console.WriteLine($" Updated: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
            Console.WriteLine($" Issues : {issueLog.FilePath}");
            Console.WriteLine();
            Console.WriteLine(" Services");
            Console.WriteLine(" -------------------------------------------------------------");

            foreach (var service in state.GetServices(ServiceHeartbeatMaxAge))
            {
                WriteStatus(service.IsOnline ? "ONLINE" : "OFFLINE", service.IsOnline ? ConsoleColor.Green : ConsoleColor.DarkGray);
                var lastSeen = service.LastSeenUtc == DateTimeOffset.MinValue
                    ? "never"
                    : service.LastSeenUtc.ToLocalTime().ToString("HH:mm:ss");
                Console.WriteLine($" {service.ServiceId,-16} role={service.Role,-8} last={lastSeen}");
            }

            Console.WriteLine();
            Console.WriteLine(" Scenarios");
            Console.WriteLine(" -------------------------------------------------------------");

            foreach (var scenario in state.GetScenarios())
            {
                WriteStatus(GetLabel(scenario.Status), GetColor(scenario.Status));
                var duration = scenario.Duration is null ? string.Empty : $" ({scenario.Duration.Value.TotalSeconds:F2}s)";
                Console.WriteLine($" {scenario.Name}{duration}");
                Console.WriteLine($"         {scenario.Details}");
            }

            Console.WriteLine(" -------------------------------------------------------------");
            Console.WriteLine(state.HasFailures ? " RESULT: FAIL" : isFinal ? " RESULT: PASS" : " RESULT: IN PROGRESS");

            if (isFinal && !state.HasFailures)
            {
                Console.WriteLine(" All integration scenarios passed.");
            }

            Console.WriteLine();
        }
    }

    private static string GetLabel(ScenarioStatus status)
    {
        return status switch
        {
            ScenarioStatus.Waiting => "WAIT",
            ScenarioStatus.Progress => "PROGRESS",
            ScenarioStatus.Passed => "PASS",
            ScenarioStatus.Failed => "FAIL",
            ScenarioStatus.Skipped => "SKIP",
            _ => status.ToString().ToUpperInvariant()
        };
    }

    private static ConsoleColor GetColor(ScenarioStatus status)
    {
        return status switch
        {
            ScenarioStatus.Progress => ConsoleColor.Yellow,
            ScenarioStatus.Passed => ConsoleColor.Green,
            ScenarioStatus.Failed => ConsoleColor.Red,
            ScenarioStatus.Skipped => ConsoleColor.DarkGray,
            _ => ConsoleColor.Gray
        };
    }

    private static void WriteStatus(string status, ConsoleColor color)
    {
        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.Write($" [{status,-8}]");
        Console.ForegroundColor = originalColor;
    }

    private static void TryClearConsole()
    {
        try
        {
            Console.Clear();
        }
        catch (IOException)
        {
        }
    }
}
