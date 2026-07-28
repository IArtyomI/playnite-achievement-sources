using System;
using System.Security.Cryptography;
using System.Text;

namespace PlayniteAchievementSources.Settings
{
    internal static class SecretProtection
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("AchievementSources.Playnite.Secret.v1");

        public static string Protect(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var plainBytes = Encoding.UTF8.GetBytes(value.Trim());
            var protectedBytes = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(protectedBytes);
        }

        public static string Unprotect(string protectedValue)
        {
            if (string.IsNullOrWhiteSpace(protectedValue))
            {
                return string.Empty;
            }

            try
            {
                var protectedBytes = Convert.FromBase64String(protectedValue);
                var plainBytes = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch (FormatException)
            {
                return string.Empty;
            }
            catch (CryptographicException)
            {
                return string.Empty;
            }
        }
    }
}
