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

Configuration file: `Config/appRegistry.json` (see example in repo)
```

Notes:
- Projects contain stub implementations; wire `IMessageRouter` and `IProcessManager` concrete registrations in `Program.cs` as needed.
- `Directory.Build.props` enforces `net8.0` and package versions.
