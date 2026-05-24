using System.Security.Cryptography;
using System.Text;

namespace QsMessaging.Shared
{
    internal abstract class NameGeneratorBase
    {
        public static string HashString(string input, int maxLength = 200)
        {
            if (string.IsNullOrEmpty(input))
            {
                throw new ArgumentException("Input string cannot be null or empty.");
            }

            if (maxLength <= 0)
            {
                throw new ArgumentException("Maximum length must be greater than 0.");
            }

            byte[] inputBytes = Encoding.UTF8.GetBytes(input);
            byte[] hashBytes = SHA256.HashData(inputBytes);
            var hashString = Convert.ToHexString(hashBytes).ToLowerInvariant();

            return hashString.Length > maxLength ? hashString.Substring(0, maxLength) : hashString;
        }
    }
}
