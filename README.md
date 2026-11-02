# TransparentLock

[![Build](https://github.com/dev-101010/TransparentLock/actions/workflows/build.yml/badge.svg)](https://github.com/dev-101010/TransparentLock/actions/workflows/build.yml)

A small Windows tray tool that locks keyboard and mouse input while the screen stays **visible**, for example for a dashboard, a running presentation or a video call you want to keep an eye on. Unlocking requires a password.

> **Not a replacement for the Windows lock screen.** TransparentLock runs without admin rights and cannot prevent every system-level action. Whenever something looks like an attempt to get around it, it hands over to the real Windows lock (Win+L).

## Features

- Transparent overlay on all monitors (per-monitor DPI aware), rebuilt when monitors change
- Lock via hotkey (default `Ctrl+Shift+L`), double-click on the tray icon or automatically after inactivity
- Unlock with a password (PBKDF2-SHA256, 100,000 iterations), escalating lockout after wrong attempts
- Optional: switch to the Windows lock after N minutes
- Tray menu with *Keep screen awake* (remembered across restarts)
- Optional start with Windows
- Light/dark theme following the Windows app mode
- English and German UI (German when the Windows display language is German)
- One instance per user and session; fast user switching and RDP work for every user

## Protection while locked

| Situation | Reaction |
|---|---|
| Ctrl, Win, Alt, Esc, F1–F12 | Windows lock |
| Injected (synthetic) key presses or clicks | Windows lock |
| Another app takes the focus (e.g. a chat app on a new message) | Focus is taken back immediately, its key presses are swallowed |
| Focus cannot be taken back within 2 s (e.g. virtual desktop switched via touchpad gesture) | Windows lock |
| Clicks on windows above the overlay (notifications, shell UI) | Swallowed |
| TransparentLock crashes while locked | Windows lock |
| Windows drops the low-level hooks after timeouts | Hooks are reinstalled every 5 s |

**Limits without admin rights:** Ctrl+Alt+Del cannot be intercepted (Ctrl already triggers the Windows lock). Windows notifications and the edge swipe UI are drawn above every application window. Someone with access to the same user account (e.g. remote management) can end the process. The screen content stays visible by design.

## Requirements

- Windows 10 (1607+) or Windows 11
- .NET Framework 4.8 (preinstalled on current Windows versions)

## Installation

Download `TransparentLock.exe` from the [latest release](https://github.com/dev-101010/TransparentLock/releases/latest) and run it; no installer is needed. The exe is not code-signed yet, so Windows SmartScreen may show *"Windows protected your PC"* on the first start. Click *More info* → *Run anyway*, or right-click the file → *Properties* → tick *Unblock*.

## Build

```
dotnet build TransparentLock.sln -c Release
```

Output: `TransparentLock/bin/Release/net48/TransparentLock.exe`

## Settings

Stored per Windows user in `%LOCALAPPDATA%\TransparentLock\settings.json`, independent of where the exe lives. Settings of version 1.x (`user.config`) are taken over once on the first start, as long as the new exe is in the same location as the old one.

The *Start with Windows* option writes `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\TransparentLock` and points to the exe that was running when the option was enabled.

## Privacy

TransparentLock does not send any data anywhere. It only stores its settings locally (see above). Details: [Privacy Policy](PRIVACY.md).

## License

[GNU General Public License v3.0](LICENSE) (GPL-3.0-only). You may use, share and modify TransparentLock, but anything you distribute that is based on it must stay open source under the same license.


---

## Deutsch (Kurzfassung)

TransparentLock sperrt Tastatur und Maus, der Bildschirm bleibt aber sichtbar. Entsperrt wird mit einem Passwort. Gesperrt wird per Tastenkürzel (Standard `Strg+Umschalt+L`), per Doppelklick auf das Tray-Icon oder automatisch bei Inaktivität.

Das Tool ist **kein Ersatz für die Windows-Sperre**. Es läuft ohne Adminrechte und wechselt zur echten Windows-Sperre, sobald etwas wie ein Umgehungsversuch aussieht: Strg, Win, Alt, künstlich erzeugte Eingaben, ein Absturz oder ein Fokusverlust, der sich nicht rückgängig machen lässt.

Die Oberfläche erscheint auf Deutsch, wenn die Windows-Anzeigesprache Deutsch ist, sonst auf Englisch. Die Einstellungen liegen pro Benutzer in `%LOCALAPPDATA%\TransparentLock\settings.json`. Das Tool sendet keine Daten.

Die exe ist noch nicht signiert. Beim ersten Start zeigt Windows SmartScreen deshalb eventuell „Der Computer wurde durch Windows geschützt“. Weiter geht es über *Weitere Informationen* → *Trotzdem ausführen*.

Lizenz: [GPL-3.0](LICENSE). Du darfst TransparentLock nutzen, weitergeben und verändern. Alles, was darauf aufbaut und weitergegeben wird, muss unter derselben Lizenz offen bleiben.
