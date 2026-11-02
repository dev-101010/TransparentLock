using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace TransparentLock
{
    /// <summary>
    /// "Start with Windows" via the per-user Run key (no admin rights required).
    /// The registry entry itself is the source of truth, not the app settings.
    /// </summary>
    public static class Autostart
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "TransparentLock";

        private static string Command
        {
            get
            {
                using var process = Process.GetCurrentProcess();
                return $"\"{process.MainModule!.FileName}\"";
            }
        }

        public static bool IsEnabled
        {
            get
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(RunKey);
                    return key?.GetValue(ValueName) is string value && value.Length > 0;
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>Returns false if the registry could not be written.</summary>
        public static bool SetEnabled(bool enabled)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RunKey);
                if (key == null)
                    return false;

                if (enabled)
                    key.SetValue(ValueName, Command);
                else
                    key.DeleteValue(ValueName, throwOnMissingValue: false);

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return false;
            }
        }
    }
}
