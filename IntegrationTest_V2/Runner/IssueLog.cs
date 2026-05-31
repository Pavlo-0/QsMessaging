using System.Text;

namespace IntegrationTestV2.Runner;

public sealed class IssueLog
{
    private readonly object _sync = new();

    public IssueLog(RunnerOptions options)
    {
        var logDirectory = Path.GetFullPath(options.LogDirectory, Environment.CurrentDirectory);
        Directory.CreateDirectory(logDirectory);
        FilePath = Path.Combine(logDirectory, $"integration-test-v2-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.log");
        File.AppendAllText(FilePath, $"{DateTimeOffset.Now:O} IntegrationTest_V2 issue log{Environment.NewLine}");
    }

    public string FilePath { get; }

    public void Write(string title, Exception exception)
    {
        var message = new StringBuilder()
            .AppendLine($"{DateTimeOffset.Now:O} ERROR {title}")
            .AppendLine(exception.ToString())
            .AppendLine()
            .ToString();

        lock (_sync)
        {
            File.AppendAllText(FilePath, message);
        }
    }
}
