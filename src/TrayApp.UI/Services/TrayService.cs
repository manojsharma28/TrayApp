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
                _manageWindow.Closed += (_, _) => _manageWindow = null;
                _manageWindow.Show();
            }
            else
            {
                _manageWindow.Activate();
            }
        });
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
        var width = Math.Max((int)window.ClientSize.Width, 320);
        var height = (int)window.ClientSize.Height;
        var x = Math.Clamp(area.Right - width - 12, area.X + 12, area.Right - width - 12);
        var y = Math.Clamp(area.Bottom - height - 12, area.Y + 12, area.Bottom - height - 12);
        window.Position = new PixelPoint(x, y);
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
