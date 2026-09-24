# TrayAppManager

This workspace contains a cross-platform tray manager solution (Avalonia UI) and a sample managed application using ZeroMQ for IPC.

Projects:
- src/TrayApp.UI — Avalonia tray interface (stub)
- src/TrayApp.Core — business logic, command dispatcher, state tracker
- src/TrayApp.Comms — ZeroMQ (NetMQ) PUB/SUB router
- src/TrayApp.ProcessControl — OS-specific process manager adapters
- src/TrayApp.Shared — shared models, interfaces, config
- samples/ManagedApp.Template — sample managed app that subscribes to control topics and publishes status

Prerequisites:
- .NET 8 SDK
- On Windows/Linux: ensure NetMQ transport is available (TCP)

Quick run (development):
From solution root run:

```bash
dotnet build
dotnet run --project src/TrayApp.Core
dotnet run --project src/TrayApp.UI
dotnet run --project samples/ManagedApp.Template

Health endpoint:

```bash
curl http://127.0.0.1:5005/health
curl http://127.0.0.1:5005/status
```

Logs are written to `logs/trayapp.log` under the running project's base directory.

Run-all scripts

Windows PowerShell: `Scripts/run-all.ps1`
Linux Bash: `Scripts/run-all.sh` (requires `jq` to parse JSON)

Configuration file: `Config/appRegistry.json` (see example in repo). The Settings window can enable or disable Windows notifications when a managed application stops. This setting is stored as `notifications.enabled` and defaults to `true` for existing configurations.
```

## Customize the UI

The Avalonia UI has three separate customization points:

- `src/TrayApp.UI/App.axaml` contains the shared theme resources. Update the brushes named `PageBackgroundBrush`, `PanelBackgroundBrush`, `PrimaryTextBrush`, `SecondaryTextBrush`, `AccentBrush`, and `InputBackgroundBrush` to change the application palette.
- `src/TrayApp.UI/Views/SettingsWindow.axaml` contains the settings page layout and its local control styles. Change the window dimensions, typography, spacing, labels, endpoint fields, and save button there. Keep the control names `PubEndpoint`, `SubEndpoint`, and `SaveButton` unless you also update `SettingsWindow.axaml.cs`.
- `src/TrayApp.UI/Services/TrayService.cs` connects the tray icon to the custom menu and owns the tray commands.
- `src/TrayApp.UI/Services/WindowsToastNotificationService.cs` sends best-effort Windows notification balloons when enabled and an application transitions to stopped.
- `src/TrayApp.UI/Views/TrayMenuWindow.axaml` contains the custom tray menu layout and styles. Update its brushes, spacing, typography, and button styles to change the menu appearance. Its code-behind, `TrayMenuWindow.axaml.cs`, creates the managed application rows and updates their status labels.

The settings page and left-click tray menu are rendered by Avalonia, so their brushes and control styles can share the application theme. The custom tray menu is compact, borderless, positioned near the bottom-right working area, and closes when it loses focus. The native OS context menu is also retained for the tray's native context-menu interaction; it uses the same labels and commands, but its colors, typography, padding, and hover treatment still follow the host OS.

Notes:
- Windows notification delivery requires Windows notifications to be enabled for the user. Notification failures are ignored so process monitoring continues normally. Linux and other platforms skip the notification.
- Projects contain stub implementations; wire `IMessageRouter` and `IProcessManager` concrete registrations in `Program.cs` as needed.
- `Directory.Build.props` enforces `net8.0` and package versions.
