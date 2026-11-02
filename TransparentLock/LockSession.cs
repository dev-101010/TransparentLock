using System;

namespace TransparentLock
{
    /// <summary>
    /// Brute-force state shared by all overlay windows, so switching to another monitor
    /// or re-locking does not reset the failed attempt counter or the lockout.
    /// </summary>
    public sealed class LockSession
    {
        private const int MaxBackoffLevel = 10;

        private int _failedAttempts;
        private int _backoffLevel;
        private DateTime _lockoutUntilUtc = DateTime.MinValue;

        public bool IsLockedOut => DateTime.UtcNow < _lockoutUntilUtc;

        public TimeSpan LockoutRemaining
        {
            get
            {
                var remaining = _lockoutUntilUtc - DateTime.UtcNow;
                return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
        }

        public int AttemptsLeft(Settings settings) =>
            Math.Max(1, settings.MaxAttemptsBeforeBackoff) - _failedAttempts;

        /// <summary>Registers a wrong password. Returns true if a lockout was started.</summary>
        public bool RegisterFailure(Settings settings)
        {
            _failedAttempts++;

            if (_failedAttempts < Math.Max(1, settings.MaxAttemptsBeforeBackoff))
                return false;

            _failedAttempts = 0;

            int baseSeconds = settings.InitialBackoffSeconds > 0 ? settings.InitialBackoffSeconds : 10;
            int backoffSeconds = baseSeconds * (1 << _backoffLevel); // doubles with every lockout
            if (_backoffLevel < MaxBackoffLevel)
                _backoffLevel++;

            _lockoutUntilUtc = DateTime.UtcNow.AddSeconds(backoffSeconds);
            return true;
        }

        public void Reset()
        {
            _failedAttempts = 0;
            _backoffLevel = 0;
            _lockoutUntilUtc = DateTime.MinValue;
        }
    }
}
