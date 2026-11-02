using System.Runtime.Serialization;

namespace TransparentLock
{
    [DataContract]
    public class Settings
    {
        public Settings()
        {
            ApplyDefaults();
        }

        // Password storage
        [DataMember] public string PasswordHash { get; set; } = string.Empty;
        [DataMember] public string PasswordSalt { get; set; } = string.Empty;

        // Time window for the password phase in seconds
        [DataMember] public int PasswordPhaseSeconds { get; set; }

        // Idle time in seconds before TransparentLock activates automatically
        // 0 or negative = disabled
        [DataMember] public int AutoLockIdleSeconds { get; set; }

        // Anti brute force
        [DataMember] public int MaxAttemptsBeforeBackoff { get; set; }
        [DataMember] public int InitialBackoffSeconds { get; set; }

        // Global hotkey to activate TransparentLock (simple string like "Ctrl+Shift+L"), empty = none
        [DataMember] public string Hotkey { get; set; } = string.Empty;

        // Minutes the transparent lock stays active before the real Windows lock takes over
        // 0 = never
        [DataMember] public int WindowsLockAfterMinutes { get; set; }

        // Last state of the "Keep screen awake" tray switch, restored on start
        [DataMember] public bool KeepAwake { get; set; }

        // The JSON serializer does not run constructors, so defaults for members
        // missing in an older settings file are applied here
        [OnDeserializing]
        private void OnDeserializing(StreamingContext context) => ApplyDefaults();

        private void ApplyDefaults()
        {
            PasswordHash = string.Empty;
            PasswordSalt = string.Empty;
            PasswordPhaseSeconds = 10;
            AutoLockIdleSeconds = 0;
            MaxAttemptsBeforeBackoff = 3;
            InitialBackoffSeconds = 10;
            Hotkey = "Ctrl+Shift+L";
            WindowsLockAfterMinutes = 0;
            KeepAwake = false;
        }
    }
}
