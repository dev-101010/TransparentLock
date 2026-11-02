using System.Text.RegularExpressions;
using System;

namespace TransparentLock
{
    public static class PasswordPolicy
    {
        // Only letters and digits to avoid layouts that require AltGr / weird combos
        private static readonly Regex AllowedPattern = new Regex("^[a-zA-Z0-9]+$",
            RegexOptions.Compiled);

        public static bool IsSafePassword(string password, out string errorMessage)
        {
            if (string.IsNullOrEmpty(password))
            {
                errorMessage = Strings.PolicyEmpty;
                return false;
            }

            // Optional: minimal length
            if (password.Length < 4)
            {
                errorMessage = Strings.PolicyTooShort;
                return false;
            }

            if (!AllowedPattern.IsMatch(password))
            {
                errorMessage = Strings.PolicyInvalidChars;
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }
    }
}