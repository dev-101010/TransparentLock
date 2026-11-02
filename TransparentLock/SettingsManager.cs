using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace TransparentLock
{
    /// <summary>
    /// Stores the settings per Windows user in %LOCALAPPDATA%\TransparentLock\settings.json.
    /// Unlike the .NET user settings (user.config), the location does not depend on the
    /// path or version of the exe, so settings survive updates and moving the program.
    /// </summary>
    public static class SettingsManager
    {
        private static readonly string SettingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TransparentLock");

        private static readonly string SettingsPath = Path.Combine(SettingsDirectory, "settings.json");

        public static Settings Current { get; private set; } = new Settings();

        public static void Load()
        {
            if (!File.Exists(SettingsPath))
            {
                // First start with the JSON file: take over settings of older versions once
                Current = LoadLegacy() ?? new Settings();
                Save();
                return;
            }

            try
            {
                using var stream = File.OpenRead(SettingsPath);
                Current = (Settings?)CreateSerializer().ReadObject(stream) ?? new Settings();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);

                // Keep the broken file for inspection instead of silently overwriting it
                try
                {
                    File.Copy(SettingsPath, SettingsPath + ".bak", overwrite: true);
                }
                catch
                {
                    // ignore
                }

                Current = new Settings();
            }
        }

        /// <summary>Returns false if the settings file could not be written.</summary>
        public static bool Save()
        {
            try
            {
                Directory.CreateDirectory(SettingsDirectory);

                // Write to a temp file first so a crash cannot leave a half-written file behind
                var tempPath = SettingsPath + ".tmp";
                using (var stream = File.Create(tempPath))
                using (var writer = JsonReaderWriterFactory.CreateJsonWriter(stream, Encoding.UTF8, ownsStream: false, indent: true))
                {
                    CreateSerializer().WriteObject(writer, Current ?? new Settings());
                }

                if (File.Exists(SettingsPath))
                    File.Replace(tempPath, SettingsPath, null);
                else
                    File.Move(tempPath, SettingsPath);

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                return false;
            }
        }

        public static bool IsPasswordConfigured()
        {
            var s = Current ?? new Settings();
            return !string.IsNullOrEmpty(s.PasswordHash) &&
                   !string.IsNullOrEmpty(s.PasswordSalt);
        }

        private static DataContractJsonSerializer CreateSerializer() =>
            new DataContractJsonSerializer(typeof(Settings));

        /// <summary>
        /// Reads the old user.config based settings (only found for the same exe path and version).
        /// Returns null if there is nothing to take over.
        /// </summary>
        private static Settings? LoadLegacy()
        {
            try
            {
                var ps = Properties.Settings.Default;
                if (string.IsNullOrEmpty(ps.PasswordHash) && string.IsNullOrEmpty(ps.Hotkey) &&
                    ps.PasswordPhaseSeconds < 0 && ps.AutoLockIdleSeconds < 0)
                    return null;

                var defaults = new Settings();

                return new Settings
                {
                    PasswordHash = ps.PasswordHash ?? string.Empty,
                    PasswordSalt = ps.PasswordSalt ?? string.Empty,
                    PasswordPhaseSeconds = ps.PasswordPhaseSeconds > -1 ? ps.PasswordPhaseSeconds : defaults.PasswordPhaseSeconds,
                    AutoLockIdleSeconds = ps.AutoLockIdleSeconds > -1 ? ps.AutoLockIdleSeconds : defaults.AutoLockIdleSeconds,
                    MaxAttemptsBeforeBackoff = ps.MaxAttemptsBeforeBackoff > -1 ? ps.MaxAttemptsBeforeBackoff : defaults.MaxAttemptsBeforeBackoff,
                    InitialBackoffSeconds = ps.InitialBackoffSeconds > -1 ? ps.InitialBackoffSeconds : defaults.InitialBackoffSeconds,
                    Hotkey = string.IsNullOrEmpty(ps.Hotkey) ? defaults.Hotkey : ps.Hotkey,
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                return null;
            }
        }
    }
}
