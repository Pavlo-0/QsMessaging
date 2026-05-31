namespace QsMessaging.Shared
{
    internal static class FailedMessageQueueName
    {
        public static string Create(string originalName, string suffix, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(originalName))
            {
                originalName = "unknown";
            }

            if (string.IsNullOrWhiteSpace(suffix))
            {
                throw new ArgumentException("Suffix can not be empty.", nameof(suffix));
            }

            if (maxLength <= suffix.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(maxLength), "Maximum length must leave room for the error suffix.");
            }

            if (originalName.Length + suffix.Length <= maxLength)
            {
                return originalName + suffix;
            }

            return NameGeneratorBase.HashString(originalName, maxLength - suffix.Length) + suffix;
        }
    }
}
