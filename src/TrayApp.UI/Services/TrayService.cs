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
    private Views.TrayMenuWindow? _trayMenuWindow;
    private NativeMenu? _nativeMenu;

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
                _nativeMenu = BuildNativeMenu(desktop);
                tray.Menu = _nativeMenu;

                tray.Clicked += (s, e) => ShowTrayMenu(desktop);

                // subscribe to state tracker updates if available
                if (_tracker != null)
                {
                    _tracker.StatusUpdated += s => Avalonia.Threading.Dispatcher.UIThread.Post(() => UpdateStatusForApp(s));
                }

                _pollTimer = new Timer(_ => _ = UpdateStatusesAsync(), null, 0, 3000);
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to initialize tray");
        }
    }

    private NativeMenu BuildNativeMenu(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var menu = new NativeMenu();

        if (!_registry.Apps.Any())
        {
            var emptyItem = new NativeMenuItem("No managed applications configured")
            {
                IsEnabled = false
            };
            menu.Items.Add(emptyItem);
        }
        else
        {
            foreach (var app in _registry.Apps)
            {
                var appItem = new NativeMenuItem(app.Name);
                var submenu = new NativeMenu();
                var startItem = new NativeMenuItem("Start application");
                startItem.Click += async (_, _) => await _controller.StartAsync(app.Id);
                var stopItem = new NativeMenuItem("Stop application");
                stopItem.Click += async (_, _) => await _controller.StopAsync(app.Id);
                var statusItem = new NativeMenuItem("Status: unknown") { IsEnabled = false };

                submenu.Items.Add(startItem);
                submenu.Items.Add(stopItem);
                submenu.Items.Add(new NativeMenuItemSeparator());
                submenu.Items.Add(statusItem);
                appItem.Menu = submenu;
                menu.Items.Add(appItem);
            }
        }

        menu.Items.Add(new NativeMenuItemSeparator());
        var settingsItem = new NativeMenuItem("Open settings");
        settingsItem.Click += (_, _) => ShowSettingsWindow();
        menu.Items.Add(settingsItem);
        var exitItem = new NativeMenuItem("Quit TrayAppManager");
        exitItem.Click += (_, _) => desktop.Shutdown();
        menu.Items.Add(exitItem);
        return menu;
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

    private void ShowTrayMenu(IClassicDesktopStyleApplicationLifetime desktop)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (_trayMenuWindow?.IsVisible == true)
            {
                _trayMenuWindow.Close();
                return;
            }

            _trayMenuWindow = new Views.TrayMenuWindow(
                _registry,
                _controller,
                _processManager,
                ShowSettingsWindow,
                () => desktop.Shutdown());
            _trayMenuWindow.Closed += (_, _) => _trayMenuWindow = null;
            _trayMenuWindow.Show();

            var screen = _trayMenuWindow.Screens.Primary;
            if (screen != null)
            {
                var area = screen.WorkingArea;
                _trayMenuWindow.Position = new PixelPoint(
                    area.Right - (int)_trayMenuWindow.ClientSize.Width - 12,
                    area.Bottom - (int)_trayMenuWindow.ClientSize.Height - 12);
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

    private async Task UpdateStatusesAsync()
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
                Avalonia.Threading.Dispatcher.UIThread.Post(() => UpdateStatusForApp(appStatus));
            }
        }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "Status update failed");
        }
    }

    private void UpdateStatusForApp(TrayApp.Shared.Models.AppStatus status)
    {
        try
        {
            _trayMenuWindow?.UpdateStatus(status.AppId, status.Status);
            if (_nativeMenu == null)
            {
                return;
            }

            foreach (var item in _nativeMenu.Items.OfType<NativeMenuItem>())
            {
                if (item.Menu is not NativeMenu submenu)
                {
                    continue;
                }

                var app = _registry.Apps.FirstOrDefault(a => a.Name == item.Header?.ToString());
                if (app?.Id != status.AppId)
                {
                    continue;
                }

                var statusItem = submenu.Items.OfType<NativeMenuItem>().LastOrDefault();
                if (statusItem != null)
                {
                    statusItem.Header = $"Status: {status.Status}";
                }
            }
        }
        catch { }
    }
}
