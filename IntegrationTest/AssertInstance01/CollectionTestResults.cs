using System.Collections.Concurrent;

namespace AssertInstance01
{
    internal static class CollectionTestResults
    {
        private static readonly ConcurrentDictionary<TestScenariousEnum, bool?> _results = new ConcurrentDictionary<TestScenariousEnum, bool?>();
        private static readonly TestScenariousEnum[] _requestResponseScenarios =
        [
            TestScenariousEnum.RequestResponse1,
            TestScenariousEnum.RequestResponse10OneByOne,
            TestScenariousEnum.RequestResponse10AtOnce,
            TestScenariousEnum.RequestResponse20AtOnceDif,
            TestScenariousEnum.TwoInstanceRequest
        ];

        public static void AddTest(TestScenariousEnum testEnum)
        {
            _results[testEnum] = null;
        }

        public static Dictionary<TestScenariousEnum, bool?> GetAllTests()
        {
            return _results.ToDictionary();
        }

        public static void FailTest(TestScenariousEnum testEnum)
        {
            _results[testEnum] = false;
        }

        public static void PassTest(TestScenariousEnum testEnum)
        {
            _results[testEnum] = true;
        }

        public static Task WaitForMessage50PausedTurnAsync()
        {
            return WaitForTerminalStateAsync(_requestResponseScenarios);
        }

        public static Task WaitForEvent50PausedTurnAsync()
        {
            return WaitForTerminalStateAsync([.. _requestResponseScenarios, TestScenariousEnum.Message50Paused]);
        }

        private static async Task WaitForTerminalStateAsync(IEnumerable<TestScenariousEnum> scenarios)
        {
            while (scenarios.Any(scenario => !_results.TryGetValue(scenario, out var result) || result is null))
            {
                await Task.Delay(100);
            }
        }
    }
}
