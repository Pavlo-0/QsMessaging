using System.Collections.Concurrent;

namespace AssertInstance01
{
    internal static class CollectionTestResults
    {
        private static readonly ConcurrentDictionary<TestScenariousEnum, bool?> _results = new ConcurrentDictionary<TestScenariousEnum, bool?>();

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
    }
}
