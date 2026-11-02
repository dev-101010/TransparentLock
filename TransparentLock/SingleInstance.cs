using System;
using System.Security.Principal;
using System.Threading;

namespace TransparentLock
{
    /// <summary>
    /// Ensures a single instance per user session. The kernel objects live in the session-local
    /// namespace ("Local\") and contain the user SID, so other users (fast user switching, RDP)
    /// can run their own instance.
    /// </summary>
    internal sealed class SingleInstance : IDisposable
    {
        private static readonly string UserId = GetUserId();
        private static string MutexName => $@"Local\TransparentLock_{UserId}";
        private static string ActivateEventName => $@"Local\TransparentLock_Activate_{UserId}";

        private readonly Mutex _mutex;
        private readonly EventWaitHandle _activateEvent;
        private readonly RegisteredWaitHandle _registration;

        private SingleInstance(Mutex mutex, EventWaitHandle activateEvent, Action onActivationRequested)
        {
            _mutex = mutex;
            _activateEvent = activateEvent;
            _registration = ThreadPool.RegisterWaitForSingleObject(_activateEvent,
                (_, _) => onActivationRequested(), null, Timeout.Infinite, executeOnlyOnce: false);
        }

        /// <summary>
        /// Returns null if another instance is already running for this user in this session.
        /// <paramref name="onActivationRequested"/> is invoked on a thread pool thread when a
        /// second instance is started.
        /// </summary>
        public static SingleInstance? TryAcquire(Action onActivationRequested)
        {
            var mutex = new Mutex(initiallyOwned: true, name: MutexName, createdNew: out bool createdNew);
            if (!createdNew)
            {
                mutex.Dispose();
                return null;
            }

            var activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);
            return new SingleInstance(mutex, activateEvent, onActivationRequested);
        }

        /// <summary>Asks the running instance to show itself.</summary>
        public static void SignalRunningInstance()
        {
            try
            {
                if (!EventWaitHandle.TryOpenExisting(ActivateEventName, out var activateEvent))
                    return;

                using (activateEvent)
                {
                    // We were just started by the user, so we may pass on the right to take the foreground
                    NativeMethods.AllowSetForegroundWindow(NativeMethods.ASFW_ANY);
                    activateEvent.Set();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }

        public void Dispose()
        {
            _registration.Unregister(null);
            _activateEvent.Dispose();

            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Not owned by this thread
            }
            _mutex.Dispose();
        }

        private static string GetUserId()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                return identity.User?.Value ?? Environment.UserName;
            }
            catch
            {
                return Environment.UserName;
            }
        }
    }
}
