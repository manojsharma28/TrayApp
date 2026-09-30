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
    private readonly WindowsToastNotificationService _toastNotifications = new();
    private readonly Dictionary<string, string> _lastStatuses = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, NativeMenuItem> _nativeStatusItems = new(StringComparer.OrdinalIgnoreCase);
    private bool _statusSnapshotReady;
    private Timer? _pollTimer;
    private readonly TrayApp.Shared.Interfaces.IStateTracker? _tracker;
    private TrayIcon? _trayIcon;
    private Views.SettingsWindow? _settingsWindow;
    private Views.ManageWindow? _manageWindow;
    private Views.TrayMenuWindow? _trayMenuWindow;
    private NativeMenu? _nativeMenu;

    public TrayService(IAppRegistry registry, IAppController controller, IProcessManager processManager, TrayApp.Shared.Interfaces.IStateTracker? tracker, ILogger<TrayService> log)
    {
        _registry = registry;
        _controller = controller;
        _processManager = processManager;
        _tracker = tracker;
        _log = log;
        UiThemeService.Apply(registry.UiTheme);
        InitializeTray();
    }

    private void InitializeTray()
    {
        try
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var icon = AppIconLoader.Load();
                var tray = new TrayIcon
                {
                    ToolTipText = "AppHive",
                    Icon = icon,
                    IsVisible = true
                };
                _trayIcon = tray;
                _nativeMenu = BuildNativeMenu(desktop);
                tray.Menu = _nativeMenu;

                tray.Clicked += (s, e) =>
                {
                    try
                    {
                        ShowTrayMenu(desktop);
                    }
                    catch (Exception ex)
                    {
                        _log.LogError(ex, "Failed to open tray menu");
                    }
                };

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
        _nativeStatusItems.Clear();
        var menu = new NativeMenu();

        var trayApps = _registry.Apps.Where(app => !app.IsHidden && !string.IsNullOrWhiteSpace(app.Id)).ToList();
        if (!trayApps.Any())
        {
            var emptyItem = new NativeMenuItem("No managed applications configured")
            {
                IsEnabled = false
            };
            menu.Items.Add(emptyItem);
        }
        else
        {
            foreach (var app in trayApps.Where(app => !string.IsNullOrWhiteSpace(app.Name)))
            {
                var appItem = new NativeMenuItem(app.Name);
                var submenu = new NativeMenu();
                var startItem = new NativeMenuItem("Start application");
                startItem.Click += async (_, _) => await _controller.StartAsync(app.Id);
                var stopItem = new NativeMenuItem("Stop application");
                stopItem.Click += async (_, _) => await _controller.StopAsync(app.Id);
                var initialStatus = _lastStatuses.TryGetValue(app.Id, out var knownStatus)
                    ? knownStatus
                    : "unknown";
                var statusItem = new NativeMenuItem($"Status: {initialStatus}") { IsEnabled = false };
                _nativeStatusItems[app.Id] = statusItem;

                submenu.Items.Add(startItem);
                submenu.Items.Add(stopItem);
                submenu.Items.Add(new NativeMenuItemSeparator());
                submenu.Items.Add(statusItem);
                appItem.Menu = submenu;
                menu.Items.Add(appItem);
            }
        }

        menu.Items.Add(new NativeMenuItemSeparator());
        var settingsItem = new NativeMenuItem("Manage applications");
        settingsItem.Click += (_, _) => ShowManageWindow();
        menu.Items.Add(settingsItem);
        var exitItem = new NativeMenuItem("Quit AppHive");
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

    private void ShowManageWindow()
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (_manageWindow is null || !_manageWindow.IsVisible)
            {
                _manageWindow = new Views.ManageWindow(_registry, _controller, _processManager, _tracker);
                _manageWindow.RegistryChanged += (_, _) => RefreshNativeMenu();
                _manageWindow.Closed += (_, _) => _manageWindow = null;
                _manageWindow.Show();
            }
            else
            {
                _manageWindow.Activate();
            }
        });
    }

    private void RefreshNativeMenu()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return;
        }

        _nativeMenu = BuildNativeMenu(desktop);
        if (_trayIcon != null)
        {
            _trayIcon.Menu = _nativeMenu;
        }
    }

    private void ShowTrayMenu(IClassicDesktopStyleApplicationLifetime desktop)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            try
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
                    ShowManageWindow,
                    () => desktop.Shutdown());
                _trayMenuWindow.Closed += (_, _) => _trayMenuWindow = null;
                _trayMenuWindow.Show();

                // Reposition after the first layout pass so the popup is never clipped.
                Avalonia.Threading.Dispatcher.UIThread.Post(() => PositionTrayMenu(_trayMenuWindow));
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to render tray menu");
            }
        });
    }

    private void PositionTrayMenu(Views.TrayMenuWindow? window)
    {
        if (window?.IsVisible != true) return;

        var screen = window.Screens.Primary;
        if (screen == null) return;

        var area = screen.WorkingArea;
        var scale = screen.Scaling;
        var availableWidth = Math.Max(1, area.Width - 24);
        var availableHeight = Math.Max(1, area.Height - 24);
        var width = Math.Min(Math.Max(window.ClientSize.Width, 320), availableWidth / scale);
        var height = Math.Min(Math.Max(window.ClientSize.Height, 320), availableHeight / scale);
        window.Width = width;
        window.Height = height;

        var pixelWidth = (int)Math.Ceiling(width * scale);
        var pixelHeight = (int)Math.Ceiling(height * scale);
        var maxX = Math.Max(area.X + 12, area.Right - pixelWidth - 12);
        var maxY = Math.Max(area.Y + 12, area.Bottom - pixelHeight - 12);
        var x = Math.Clamp(area.Right - pixelWidth - 12, area.X + 12, maxX);
        var y = Math.Clamp(area.Bottom - pixelHeight - 12, area.Y + 12, maxY);
        window.Position = new PixelPoint(x, y);
    }

    private async Task UpdateStatusesAsync()
    {
        try
        {
            foreach (var app in _registry.Apps)
            {
                var trackerStatus = _tracker?.GetStatus(app.Id);
                var hasProcessCommand = !string.IsNullOrWhiteSpace(app.StartCommand)
                    || !string.IsNullOrWhiteSpace(app.ExecutablePath);
                if (hasProcessCommand)
                {
                    var running = await _processManager.IsRunningAsync(app.Id);
                    trackerStatus = running ? "running" : "stopped";
                }

                var appStatus = new TrayApp.Shared.Models.AppStatus(app.Id, trackerStatus ?? "stopped", DateTime.UtcNow);
                Avalonia.Threading.Dispatcher.UIThread.Post(() => UpdateStatusForApp(appStatus));
            }

            Avalonia.Threading.Dispatcher.UIThread.Post(() => _statusSnapshotReady = true);
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
            var currentStatus = status.Status.Trim().ToLowerInvariant();
            var isStopped = currentStatus is "stopped" or "not running" or "exited";
            if (_statusSnapshotReady
                && _lastStatuses.TryGetValue(status.AppId, out var previousStatus)
                && previousStatus is not ("stopped" or "not running" or "exited")
                && isStopped
                && _registry.NotificationsEnabled)
            {
                var appName = _registry.GetApp(status.AppId)?.Name ?? status.AppId;
                _toastNotifications.ShowApplicationStopped(appName);
            }

            _lastStatuses[status.AppId] = currentStatus;
            _trayMenuWindow?.UpdateStatus(status.AppId, status.Status);
            if (_nativeStatusItems.TryGetValue(status.AppId, out var statusItem))
            {
                statusItem.Header = $"Status: {status.Status}";
            }
        }
        catch { }
    }
}
