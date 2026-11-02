using System.Security.Cryptography;
using System;

namespace TransparentLock
{
    public static class PasswordHelper
    {
        private const int SaltSize = 16; // 128 bit
        private const int HashSize = 32; // 256 bit
        private const int Iterations = 100_000;

        public static void SetPassword(Settings settings, string plainPassword)
        {
            if (string.IsNullOrEmpty(plainPassword))
            {
                settings.PasswordHash = string.Empty;
                settings.PasswordSalt = string.Empty;
                return;
            }

            var saltBytes = new byte[SaltSize];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(saltBytes);
            }
            var hashBytes = HashPasswordInternal(plainPassword, saltBytes);

            settings.PasswordSalt = Convert.ToBase64String(saltBytes);
            settings.PasswordHash = Convert.ToBase64String(hashBytes);
        }

        public static bool VerifyPassword(Settings settings, string plainPassword)
        {
            if (string.IsNullOrEmpty(settings.PasswordHash) || string.IsNullOrEmpty(settings.PasswordSalt))
            {
                // No password configured -> deny unlock
                return false;
            }

            byte[] saltBytes, expectedHash;
            try
            {
                saltBytes = Convert.FromBase64String(settings.PasswordSalt);
                expectedHash = Convert.FromBase64String(settings.PasswordHash);
            }
            catch (FormatException)
            {
                // Corrupted settings -> deny unlock
                return false;
            }

            var actualHash = HashPasswordInternal(plainPassword, saltBytes);

            if (actualHash.Length != expectedHash.Length)
                return false;

            // Timing-safe comparison
            var diff = 0;
            for (int i = 0; i < expectedHash.Length; i++)
            {
                diff |= expectedHash[i] ^ actualHash[i];
            }

            return diff == 0;
        }

        private static byte[] HashPasswordInternal(string plainPassword, byte[] salt)
        {
            using var deriveBytes = new Rfc2898DeriveBytes(
                plainPassword,
                salt,
                Iterations,
                HashAlgorithmName.SHA256);

            return deriveBytes.GetBytes(HashSize);
        }
    }
}