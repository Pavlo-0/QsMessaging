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

            using (var sha256 = SHA256.Create())
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(input);
                byte[] hashBytes = sha256.ComputeHash(inputBytes);

                var hashString = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();

                return hashString.Length > maxLength ? hashString.Substring(0, maxLength) : hashString;
            }
        }
    }
}
