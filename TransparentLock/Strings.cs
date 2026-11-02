using System.Globalization;

namespace TransparentLock
{
    /// <summary>
    /// UI texts in English and German. German is used when the Windows display language is German,
    /// English otherwise. Used from XAML via {x:Static local:Strings.Name}.
    /// </summary>
    public static class Strings
    {
        public static readonly bool IsGerman = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "de";

        private static string T(string en, string de) => IsGerman ? de : en;

        // ----- Common -----
        public static string AppName => "TransparentLock";

        // ----- Tray -----
        public static string TrayLockNow => T("Lock now", "Jetzt sperren");
        public static string TrayKeepAwake => T("Keep screen awake", "Bildschirm wach halten");
        public static string TraySettings => T("Settings", "Einstellungen");
        public static string TrayExit => T("Exit", "Beenden");
        public static string TrayStatusNoPassword => T("No password set", "Kein Passwort gesetzt");
        public static string TrayStatusReady => T("Ready", "Bereit");
        public static string TrayStatusAutoLock(string duration) =>
            T($"Ready · auto-lock after {duration}", $"Bereit · Auto-Sperre nach {duration}");
        public static string TrayTooltipNoPassword => T("TransparentLock – no password set", "TransparentLock – kein Passwort");

        public static string BalloonHotkeyTaken(string hotkey) => T(
            $"The hotkey {hotkey} is used by another application. Choose a different one in the settings.",
            $"Der Hotkey {hotkey} wird von einer anderen Anwendung verwendet. Bitte in den Einstellungen einen anderen wählen.");
        public static string BalloonSetPasswordFirst => T("Set a password before locking.", "Vor dem Sperren bitte ein Passwort festlegen.");
        public static string BalloonHookFailed => T(
            "Global keyboard hook could not be initialized.\nKeyboard protection is limited.",
            "Der globale Tastatur-Hook konnte nicht eingerichtet werden.\nDer Tastaturschutz ist eingeschränkt.");

        // ----- Lock overlay -----
        public static string OverlayTitle => T("Display locked", "Bildschirm gesperrt");
        public static string OverlayHint => T("Press any key or click to unlock", "Zum Entsperren eine Taste drücken oder klicken");
        public static string OverlayEnterPassword => T("Enter password", "Passwort eingeben");
        public static string OverlayChecking => T("Checking…", "Wird geprüft…");
        public static string OverlayLockout(double seconds) => T(
            $"Too many failed attempts. Try again in {seconds} s.",
            $"Zu viele Fehlversuche. Erneut versuchen in {seconds} s.");
        public static string OverlayWrongPassword(int attemptsLeft) => attemptsLeft == 1
            ? T("Wrong password. 1 attempt left before lockout.", "Falsches Passwort. Noch 1 Versuch vor der Sperre.")
            : T($"Wrong password. {attemptsLeft} attempts left before lockout.", $"Falsches Passwort. Noch {attemptsLeft} Versuche vor der Sperre.");

        // ----- Settings window -----
        public static string SettingsTitle => T("TransparentLock Settings", "TransparentLock Einstellungen");
        public static string SettingsSubtitle => T("Lock input while keeping the screen visible", "Eingaben sperren, Bildschirm bleibt sichtbar");
        public static string NoticeTitle => T("Not a replacement for the Windows lock screen", "Kein Ersatz für den Windows-Sperrbildschirm");
        public static string NoticeText => T(
            "TransparentLock is a user-mode application and cannot prevent every system-level action. For full protection use the Windows lock screen (Win+L).",
            "TransparentLock läuft ohne Administratorrechte und kann nicht jede Aktion auf Systemebene verhindern. Für vollständigen Schutz den Windows-Sperrbildschirm verwenden (Win+L).");

        public static string SectionPassword => T("Password", "Passwort");
        public static string SectionLocking => T("Locking", "Sperren");
        public static string SectionBruteForce => T("Brute-force protection", "Schutz vor Durchprobieren");
        public static string SectionGeneral => T("General", "Allgemein");

        public static string PasswordTitle => T("Unlock password", "Passwort zum Entsperren");
        public static string PasswordStatusSet => T("Required to unlock the display.", "Wird zum Entsperren benötigt.");
        public static string PasswordStatusNotSet => T("Set a password to enable locking.", "Ohne Passwort kann nicht gesperrt werden.");
        public static string PillSet => T("Set", "Gesetzt");
        public static string PillNotSet => T("Not set", "Nicht gesetzt");
        public static string Remove => T("Remove", "Entfernen");
        public static string NewPasswordTitle => T("New password", "Neues Passwort");
        public static string NewPasswordDescription => T("At least 4 characters, letters and digits only.", "Mindestens 4 Zeichen, nur Buchstaben und Ziffern.");
        public static string ConfirmPasswordTitle => T("Confirm password", "Passwort bestätigen");
        public static string ConfirmPasswordDescription => T("Leave both fields empty to keep the current password.", "Beide Felder leer lassen, um das aktuelle Passwort zu behalten.");

        public static string HotkeyTitle => T("Lock hotkey", "Tastenkürzel zum Sperren");
        public static string HotkeyDescription => T(
            "Click the field and press a key combination. Backspace removes the hotkey.",
            "Ins Feld klicken und eine Tastenkombination drücken. Rücktaste entfernt das Kürzel.");
        public static string HotkeyNone => T("None", "Keins");
        public static string HotkeyPressKeys => T("Press keys…", "Tasten drücken…");
        public static string HotkeyNeedsModifier => T("Add Ctrl, Alt, Shift or Win", "Strg, Alt, Umschalt oder Win dazu");

        public static string AutoLockTitle => T("Auto-lock after inactivity", "Automatisch sperren bei Inaktivität");
        public static string AutoLockDescription => T("Seconds without input. 0 turns auto-lock off.", "Sekunden ohne Eingabe. 0 schaltet die Auto-Sperre aus.");
        public static string PromptDurationTitle => T("Password prompt duration", "Anzeigedauer der Passworteingabe");
        public static string PromptDurationDescription => T("Seconds the password field stays open (min. 5).", "Sekunden, die das Passwortfeld offen bleibt (min. 5).");
        public static string WindowsLockTitle => T("Switch to Windows lock after", "Zur Windows-Sperre wechseln nach");
        public static string WindowsLockDescription => T(
            "Minutes until the session is locked by Windows itself. 0 = never.",
            "Minuten, bis Windows die Sitzung selbst sperrt. 0 = nie.");

        public static string AttemptsTitle => T("Attempts before lockout", "Versuche bis zur Wartezeit");
        public static string AttemptsDescription => T(
            "Wrong passwords allowed before input is blocked (min. 1).",
            "Erlaubte Fehlversuche, bevor die Eingabe blockiert wird (min. 1).");
        public static string BackoffTitle => T("Initial lockout", "Erste Wartezeit");
        public static string BackoffDescription => T(
            "Seconds (min. 3). Doubles with every further lockout.",
            "Sekunden (min. 3). Verdoppelt sich bei jeder weiteren Sperre.");

        public static string AutostartTitle => T("Start with Windows", "Mit Windows starten");
        public static string AutostartDescription => T("Run TransparentLock automatically when you sign in.", "TransparentLock bei der Anmeldung automatisch starten.");
        public static string KeepAwakeTitle => T("Keep screen awake", "Bildschirm wach halten");
        public static string KeepAwakeDescription => T(
            "Prevents the display from turning off and the PC from sleeping. Same switch as in the tray menu; remembered across restarts.",
            "Verhindert, dass sich der Bildschirm abschaltet und der PC in den Ruhezustand geht. Gleicher Schalter wie im Tray-Menü, bleibt nach einem Neustart erhalten.");

        public static string On => T("On", "Ein");
        public static string Off => T("Off", "Aus");
        public static string Cancel => T("Cancel", "Abbrechen");
        public static string Save => T("Save", "Speichern");

        public static string ErrorNumber(string label, int min) => T(
            $"{label} must be a whole number of at least {min}.",
            $"{label}: bitte eine ganze Zahl ab {min} eingeben.");
        public static string ErrorPasswordsDontMatch => T("The passwords do not match.", "Die Passwörter stimmen nicht überein.");
        public static string ErrorHotkeyTaken(string hotkey) => T(
            $"{hotkey} is already used by another application.",
            $"{hotkey} wird bereits von einer anderen Anwendung verwendet.");
        public static string ErrorAutostart => T("The autostart setting could not be changed.", "Der Autostart konnte nicht geändert werden.");
        public static string ErrorSave => T("The settings could not be saved.", "Die Einstellungen konnten nicht gespeichert werden.");

        public static string RemovePasswordQuestion => T(
            "Remove the current TransparentLock password?\nTransparentLock will not lock until a new password is set.",
            "Das aktuelle TransparentLock-Passwort entfernen?\nTransparentLock sperrt erst wieder, wenn ein neues Passwort gesetzt ist.");

        // ----- Password policy -----
        public static string PolicyEmpty => T("Password must not be empty.", "Das Passwort darf nicht leer sein.");
        public static string PolicyTooShort => T("Password must be at least 4 characters long.", "Das Passwort muss mindestens 4 Zeichen lang sein.");
        public static string PolicyInvalidChars => T(
            "Password may only contain letters (a-z, A-Z) and digits (0-9).\nPlease avoid special characters that require AltGr or special keys.",
            "Das Passwort darf nur Buchstaben (a-z, A-Z) und Ziffern (0-9) enthalten.\nSonderzeichen, die AltGr oder Spezialtasten benötigen, bitte vermeiden.");

        // ----- Key names for display (stored hotkeys always use the English names) -----
        public static string KeyCtrl => T("Ctrl", "Strg");
        public static string KeyShift => T("Shift", "Umschalt");
    }
}
