using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Microsoft.Extensions.Logging;
using Avalonia;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using TrayApp.Shared.Interfaces;

namespace TrayApp.UI.Services;

public class TrayService
{
    private readonly IAppRegistry _registry;
    private readonly IAppController _controller;
    private readonly IProcessManager _processManager;
    private readonly ILogger<TrayService> _log;
    private Timer? _pollTimer;
    private readonly TrayApp.Shared.Interfaces.IStateTracker? _tracker;
    private TrayIcon? _trayIcon;
    private Views.SettingsWindow? _settingsWindow;

    public TrayService(IAppRegistry registry, IAppController controller, IProcessManager processManager, TrayApp.Shared.Interfaces.IStateTracker? tracker, ILogger<TrayService> log)
    {
        _registry = registry;
        _controller = controller;
        _processManager = processManager;
        _tracker = tracker;
        _log = log;
        InitializeTray();
    }

    private void InitializeTray()
    {
        try
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var icon = LoadTrayIcon();
                var tray = new TrayIcon
                {
                    ToolTipText = "TrayAppManager",
                    Icon = icon,
                    IsVisible = true
                };
                _trayIcon = tray;

                var menu = new NativeMenu();

                if (!_registry.Apps.Any())
                {
                    var noAppsItem = new NativeMenuItem("No apps configured");
                    noAppsItem.IsEnabled = false;
                    menu.Items.Add(noAppsItem);
                }
                else
                {
                    foreach (var app in _registry.Apps)
                    {
                        var appMenu = new NativeMenuItem(app.Name);
                        var submenu = new NativeMenu();

                        var startItem = new NativeMenuItem("Start");
                        startItem.Click += async (s, e) => await _controller.StartAsync(app.Id);
                        submenu.Items.Add(startItem);

                        var stopItem = new NativeMenuItem("Stop");
                        stopItem.Click += async (s, e) => await _controller.StopAsync(app.Id);
                        submenu.Items.Add(stopItem);

                        var statusItem = new NativeMenuItem($"Status: {{unknown}}") { IsEnabled = false };
                        submenu.Items.Add(new NativeMenuItemSeparator());
                        submenu.Items.Add(statusItem);

                        appMenu.Menu = submenu;
                        menu.Items.Add(appMenu);
                    }
                }

                var exitItem = new NativeMenuItem("Exit");
                exitItem.Click += (s, e) => desktop.Shutdown();
                menu.Items.Add(new NativeMenuItemSeparator());
                var settingsItem = new NativeMenuItem("Settings");
                settingsItem.Click += (s, e) => ShowSettingsWindow();
                menu.Items.Add(settingsItem);
                menu.Items.Add(exitItem);

                tray.Clicked += (s, e) => ShowSettingsWindow();
                tray.Menu = menu;

                // subscribe to state tracker updates if available
                if (_tracker != null)
                {
                    _tracker.StatusUpdated += s => Avalonia.Threading.Dispatcher.UIThread.Post(() => UpdateStatusForApp(menu, s));
                }

                _pollTimer = new Timer(_ => _ = UpdateStatusesAsync(menu), null, 0, 3000);
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to initialize tray");
        }
    }

    private void ShowSettingsWindow()
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (_settingsWindow is null || !(_settingsWindow.IsVisible || _settingsWindow.IsActive))
            {
                _settingsWindow = new Views.SettingsWindow(_registry);
                _settingsWindow.Closed += (_, _) => _settingsWindow = null;
                _settingsWindow.Show();
            }
            else
            {
                _settingsWindow.Activate();
                _settingsWindow.Show();
            }
        });
    }

    private WindowIcon? LoadTrayIcon()
    {
        try
        {
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "Assets", "trayapp-icon.png"),
                Path.Combine(AppContext.BaseDirectory, "trayapp-icon.png"),
                Path.Combine(Directory.GetCurrentDirectory(), "Assets", "trayapp-icon.png")
            };

            foreach (var path in candidates)
            {
                if (File.Exists(path))
                {
                    return new WindowIcon(path);
                }
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Unable to load tray icon asset.");
        }

        return null;
    }

    private async Task UpdateStatusesAsync(NativeMenu menu)
    {
        try
        {
            foreach (var app in _registry.Apps)
            {
                var status = _tracker?.GetStatus(app.Id);
                if (string.IsNullOrWhiteSpace(status))
                {
                    var running = await _processManager.IsRunningAsync(app.Id);
                    status = running ? "running" : "stopped";
                }

                var appStatus = new TrayApp.Shared.Models.AppStatus(app.Id, status, DateTime.UtcNow);
                Avalonia.Threading.Dispatcher.UIThread.Post(() => UpdateStatusForApp(menu, appStatus));
            }
        }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "Status update failed");
        }
    }

    private void UpdateStatusForApp(NativeMenu menu, TrayApp.Shared.Models.AppStatus status)
    {
        try
        {
            foreach (var item in menu.Items.OfType<NativeMenuItem>())
            {
                if (item.Menu is NativeMenu sub)
                {
                    var appName = item.Header?.ToString();
                    var app = _registry.Apps.FirstOrDefault(a => a.Name == appName);
                    if (app == null) continue;
                    if (app.Id != status.AppId) continue;
                    var statusItem = sub.Items.OfType<NativeMenuItem>().LastOrDefault();
                    if (statusItem != null)
                    {
                        statusItem.Header = $"Status: {status.Status}";
                    }
                }
            }
        }
        catch { }
    }
}
