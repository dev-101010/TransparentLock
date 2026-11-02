# Privacy Policy

*Last updated: 2026-10-23*

TransparentLock does not collect, transmit or share any data. It has no network functionality, no telemetry, no analytics and no update checks.

## Data stored locally

TransparentLock only stores the following data on your own computer, for your Windows user account:

| Data | Location | Purpose |
|---|---|---|
| Settings (timeouts, hotkey, options) | `%LOCALAPPDATA%\TransparentLock\settings.json` | Remember your configuration |
| Password hash and salt (PBKDF2-SHA256), never the password itself | same file | Verify the unlock password |
| Autostart entry (only if *Start with Windows* is enabled) | `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\TransparentLock` | Start the app when you sign in |

## Keyboard and mouse input

While the screen is locked, TransparentLock uses low-level keyboard and mouse hooks to block input and to detect bypass attempts. Input is evaluated in memory only. It is never logged, stored or transmitted. The hooks are removed as soon as the screen is unlocked.

## Removing all data

Delete the folder `%LOCALAPPDATA%\TransparentLock` and disable *Start with Windows* in the settings (or delete the registry value above).

## Contact

Questions about this policy: [open an issue](https://github.com/dev-101010/TransparentLock/issues).

---

## Datenschutz (Deutsch)

TransparentLock erhebt, überträgt und teilt keine Daten. Das Programm hat keine Netzwerkfunktionen, keine Telemetrie, keine Analyse und keine Update-Prüfung.

Lokal und nur für dein Windows-Benutzerkonto gespeichert werden die Einstellungen sowie Hash und Salt des Passworts (nie das Passwort selbst) in `%LOCALAPPDATA%\TransparentLock\settings.json`. Ist *Mit Windows starten* aktiviert, kommt ein Autostart-Eintrag unter `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` dazu.

Tastatur- und Mauseingaben werden nur während der Sperre im Arbeitsspeicher ausgewertet, um Eingaben zu blockieren und Umgehungsversuche zu erkennen. Sie werden nie protokolliert, gespeichert oder übertragen.

Alle Daten entfernen: den Ordner `%LOCALAPPDATA%\TransparentLock` löschen und *Mit Windows starten* in den Einstellungen ausschalten.
