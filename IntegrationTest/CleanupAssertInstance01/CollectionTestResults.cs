using System.Collections.Concurrent;

namespace CleanupAssertInstance01;

internal static class CollectionTestResults
{
    private static readonly ConcurrentDictionary<CleanupTestScenario, bool?> Results = new();

    public static void AddTest(CleanupTestScenario testScenario)
    {
        Results[testScenario] = null;
    }

    public static Dictionary<CleanupTestScenario, bool?> GetAllTests()
    {
        return Results.ToDictionary();
    }

    public static void PassTest(CleanupTestScenario testScenario)
    {
        Results[testScenario] = true;
    }

    public static void FailTest(CleanupTestScenario testScenario)
    {
        Results[testScenario] = false;
    }
}
