using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using TrayApp.Shared.Interfaces;
using TrayApp.Shared.Models;
using TrayApp.UI.Services;

namespace TrayApp.UI.Views;

public partial class ManageWindow : Window
{
    private readonly IAppRegistry _registry;
    private readonly IAppController _controller;
    private readonly IProcessManager _processManager;
    private readonly Dictionary<string, TextBlock> _statusLabels = new();
    private readonly Dictionary<string, Ellipse> _statusDots = new();
    private string? _editingAppId;

    public event EventHandler? RegistryChanged;

    public ManageWindow(IAppRegistry registry, IAppController controller, IProcessManager processManager, IStateTracker? tracker = null)
    {
        _registry = registry;
        _controller = controller;
        _processManager = processManager;
        InitializeComponent();
        Icon = AppIconLoader.Load();
        UiThemeService.Apply(registry.UiTheme);

        PubEndpointBox.Text = registry.PubEndpoint;
        SubEndpointBox.Text = registry.SubEndpoint;
        ThemeSelector.SelectedIndex = string.Equals(registry.UiTheme, "blue", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        ThemeSelector.SelectionChanged += (_, _) =>
        {
            var theme = (ThemeSelector.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "natural";
            _registry.SetUiTheme(theme);
            UiThemeService.Apply(theme);
        };
        ApplyStaticButtonHover(RefreshButton, isPrimary: false);
        ApplyStaticButtonHover(AddButton, isPrimary: true);
        ApplyStaticButtonHover(CancelEditButton, isPrimary: false);
        ApplyStaticButtonHover(SaveConfigButton, isPrimary: true);
        RefreshButton.Click += async (_, _) => await RefreshStatusesAsync();
        AddButton.Click += AddButton_Click;
        CancelEditButton.Click += (_, _) => ResetForm();
        SaveConfigButton.Click += SaveConfigButton_Click;
        if (tracker != null)
            tracker.StatusUpdated += status => Avalonia.Threading.Dispatcher.UIThread.Post(() => SetStatus(status.AppId, status.Status));
        RenderApps();
        _ = RefreshStatusesAsync();
    }

    private static void ApplyStaticButtonHover(Button button, bool isPrimary)
    {
        var application = Application.Current!;
        var normalForeground = isPrimary ? Brushes.White : (IBrush)application.FindResource("PrimaryTextBrush")!;

        if (button.Content is string text)
        {
            button.Content = new TextBlock
            {
                Text = text,
                Foreground = normalForeground
            };
        }

        button.PointerEntered += (_, _) =>
        {
            button.Background = (IBrush)application.FindResource(isPrimary ? "AccentHoverBrush" : "ActionHoverBrush")!;
            button.BorderBrush = (IBrush)application.FindResource("AccentBrush")!;
            button.BorderThickness = new Thickness(1);
            button.Foreground = Brushes.White;
            if (button.Content is TextBlock text)
            {
                text.Foreground = Brushes.White;
            }
        };
        button.PointerExited += (_, _) =>
        {
            button.Background = (IBrush)application.FindResource(isPrimary ? "AccentBrush" : "ActionButtonBrush")!;
            button.BorderBrush = (IBrush)application.FindResource("ActionBorderBrush")!;
            button.BorderThickness = new Thickness(1);
            button.Foreground = normalForeground;
            if (button.Content is TextBlock text)
            {
                text.Foreground = normalForeground;
            }
        };
    }

    private void RenderApps()
    {
        AppsPanel.Children.Clear();
        _statusLabels.Clear();
        _statusDots.Clear();
        var apps = _registry.Apps
            .OrderBy(app => app.Category)
            .ThenBy(app => app.Name)
            .ToList();

        foreach (var group in apps.GroupBy(app => string.IsNullOrWhiteSpace(app.Category) ? "Services" : app.Category))
        {
            AppsPanel.Children.Add(new TextBlock
            {
                Text = group.Key.ToUpperInvariant(),
                FontSize = 11,
                FontWeight = FontWeight.Bold,
                Foreground = (IBrush)Application.Current!.FindResource("AccentBrush")!,
                LetterSpacing = 1.3,
                Margin = new Thickness(2, 4, 0, -8)
            });
            foreach (var app in group)
                AppsPanel.Children.Add(CreateAppRow(app));
        }

        if (apps.Count == 0)
        {
            AppsPanel.Children.Add(new TextBlock
            {
                Text = "No applications registered yet.",
                Foreground = (IBrush)Application.Current!.FindResource("SecondaryTextBrush")!,
                Margin = new Thickness(4, 14)
            });
        }
    }

    private Control CreateAppRow(AppInfo app)
    {
        var dot = new Ellipse { Width = 10, Height = 10, Fill = StatusBrush("unknown"), VerticalAlignment = VerticalAlignment.Center };
        var status = new TextBlock { Text = "UNKNOWN", FontSize = 11, FontWeight = FontWeight.SemiBold, Foreground = (IBrush)Application.Current!.FindResource("SecondaryTextBrush")!, VerticalAlignment = VerticalAlignment.Center };
        _statusDots[app.Id] = dot;
        _statusLabels[app.Id] = status;

        var start = ActionButton("Start", "Start application", async () =>
        {
            SetStatus(app.Id, "starting");
            await _controller.StartAsync(app.Id);
            await RefreshAppStatusAsync(app.Id, expectRunning: true);
        });
        var stop = ActionButton("Stop", "Stop application", async () => { await _controller.StopAsync(app.Id); SetStatus(app.Id, "stopped"); });
        var restart = ActionButton("Restart", "Stop and start application", async () =>
        {
            SetStatus(app.Id, "restarting");
            await _controller.RestartAsync(app.Id);
            await RefreshAppStatusAsync(app.Id, expectRunning: true);
        });
        var hide = ActionButton(app.IsHidden ? "Show" : "Hide", app.IsHidden ? "Show application in tray" : "Hide application from tray", () =>
        {
            _registry.SetHidden(app.Id, !app.IsHidden);
            RegistryChanged?.Invoke(this, EventArgs.Empty);
            RenderApps();
            return Task.CompletedTask;
        });
        var delete = ActionButton("Delete", "Remove application from registry", () =>
        {
            _registry.RemoveApp(app.Id);
            RegistryChanged?.Invoke(this, EventArgs.Empty);
            RenderApps();
            return Task.CompletedTask;
        });

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, Children = { start, stop, restart, hide, delete } };
        var identity = new StackPanel { Spacing = 2, Children = {
            new TextBlock { Text = app.Name, FontSize = 15, FontWeight = FontWeight.SemiBold, Foreground = (IBrush)Application.Current!.FindResource("PrimaryTextBrush")! },
            new TextBlock { Text = app.Id, FontSize = 11, Foreground = (IBrush)Application.Current!.FindResource("SecondaryTextBrush")! }
        } };
        var appName = (TextBlock)identity.Children[0];
        appName.Cursor = new Cursor(StandardCursorType.Hand);
        appName.PointerPressed += (_, e) =>
        {
            if (e.ClickCount == 2)
            {
                BeginEdit(app);
                e.Handled = true;
            }
        };
        var statusPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7, Children = { dot, status } };
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        header.Children.Add(identity);
        Grid.SetColumn(statusPanel, 1);
        header.Children.Add(statusPanel);
        var content = new StackPanel { Spacing = 8, Children = { header, actions } };
        var editItem = new MenuItem { Header = "Edit" };
        editItem.Click += (_, _) => BeginEdit(app);
        var visibilityItem = new MenuItem { Header = app.IsHidden ? "Show in tray" : "Hide from tray" };
        visibilityItem.Click += (_, _) =>
        {
            _registry.SetHidden(app.Id, !app.IsHidden);
            RegistryChanged?.Invoke(this, EventArgs.Empty);
            RenderApps();
        };
        var deleteItem = new MenuItem { Header = "Delete" };
        deleteItem.Click += (_, _) =>
        {
            _registry.RemoveApp(app.Id);
            RegistryChanged?.Invoke(this, EventArgs.Empty);
            RenderApps();
        };
        return new Border
        {
            Background = (IBrush)Application.Current!.FindResource("InputBackgroundBrush")!,
            BorderBrush = (IBrush)Application.Current!.FindResource("PanelBorderBrush")!,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(14),
            Child = content,
            ContextMenu = new ContextMenu { Items = { editItem, visibilityItem, new Separator(), deleteItem } }
        };
    }

    private Button ActionButton(string text, string tooltip, Func<Task> action)
    {
        var button = new Button { Content = ActionButtonText(text), Classes = { "action" } };
        ToolTip.SetTip(button, tooltip);
        button.Click += async (_, _) => await action();
        ApplyActionButtonHover(button);
        return button;
    }

    private static void ApplyActionButtonHover(Button button)
    {
        var application = Application.Current!;
        var normalBackground = (IBrush)application.FindResource("ActionButtonBrush")!;
        var normalBorder = (IBrush)application.FindResource("ActionBorderBrush")!;
        var normalForeground = (IBrush)application.FindResource("PrimaryTextBrush")!;
        var hoverBackground = (IBrush)application.FindResource("ActionHoverBrush")!;
        var hoverBorder = (IBrush)application.FindResource("AccentBrush")!;
        var hoverForeground = Brushes.White;

        button.PointerEntered += (_, _) =>
        {
            button.Background = hoverBackground;
            button.BorderBrush = hoverBorder;
            button.BorderThickness = new Thickness(1);
            button.Foreground = hoverForeground;
            if (button.Content is TextBlock text)
            {
                text.Foreground = hoverForeground;
            }
        };
        button.PointerExited += (_, _) =>
        {
            button.Background = normalBackground;
            button.BorderBrush = normalBorder;
            button.BorderThickness = new Thickness(1);
            button.Foreground = normalForeground;
            if (button.Content is TextBlock text)
            {
                text.Foreground = normalForeground;
            }
        };
    }

    private static TextBlock ActionButtonText(string text)
    {
        return new TextBlock
        {
            Text = text,
            Foreground = (IBrush)Application.Current!.FindResource("PrimaryTextBrush")!
        };
    }

    private async Task RefreshStatusesAsync()
    {
        foreach (var app in _registry.Apps)
        {
            var running = await _processManager.IsRunningAsync(app.Id);
            SetStatus(app.Id, running ? "running" : "stopped");
        }
    }

    private async Task RefreshAppStatusAsync(string appId, bool expectRunning)
    {
        var running = false;
        for (var attempt = 0; attempt < 10; attempt++)
        {
            running = await _processManager.IsRunningAsync(appId);
            if (running == expectRunning)
            {
                break;
            }

            await Task.Delay(200);
        }

        SetStatus(appId, running ? "running" : "stopped");
    }

    private void SetStatus(string appId, string value)
    {
        if (_statusLabels.TryGetValue(appId, out var label))
        {
            label.Text = value.ToUpperInvariant();
            label.Foreground = StatusBrush(value);
        }
        if (_statusDots.TryGetValue(appId, out var dot)) dot.Fill = StatusBrush(value);
    }

    private static IBrush StatusBrush(string status) => status.ToLowerInvariant() switch
    {
        "running" => (IBrush)Application.Current!.FindResource("RunningStatusBrush")!,
        "stopped" => (IBrush)Application.Current!.FindResource("StoppedStatusBrush")!,
        "restarting" or "starting" => (IBrush)Application.Current!.FindResource("LavenderAccentBrush")!,
        "error" => (IBrush)Application.Current!.FindResource("TerracottaAccentBrush")!,
        _ => (IBrush)Application.Current!.FindResource("MutedStatusBrush")!
    };

    private void AddButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var category = (CategoryBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Services";
            var app = new AppInfo(AppIdBox.Text ?? string.Empty, AppNameBox.Text ?? string.Empty, StartCommand: CommandBox.Text, ZmqEndpoint: EndpointBox.Text, Category: category);
            if (_editingAppId == null)
            {
                _registry.AddApp(app);
                FormMessage.Text = "Application added.";
            }
            else
            {
                var existing = _registry.GetApp(_editingAppId);
                _registry.UpdateApp(app with
                {
                    Id = _editingAppId,
                    ExecutablePath = existing?.ExecutablePath,
                    Args = existing?.Args,
                    IsHidden = existing?.IsHidden ?? false
                });
                FormMessage.Text = "Application updated.";
            }
            RegistryChanged?.Invoke(this, EventArgs.Empty);
            ResetForm(false);
            RenderApps();
        }
        catch (Exception ex)
        {
            FormMessage.Text = ex.Message;
        }
    }

    private void BeginEdit(AppInfo app)
    {
        _editingAppId = app.Id;
        AppIdBox.Text = app.Id;
        AppIdBox.IsEnabled = false;
        AppNameBox.Text = app.Name;
        CommandBox.Text = app.StartCommand ?? app.ExecutablePath ?? string.Empty;
        EndpointBox.Text = app.ZmqEndpoint ?? string.Empty;
        CategoryBox.SelectedItem = CategoryBox.Items.OfType<ComboBoxItem>().FirstOrDefault(item => string.Equals(item.Content?.ToString(), app.Category, StringComparison.OrdinalIgnoreCase));
        FormTitle.Text = "Edit application";
        AddButton.Content = "Save changes";
        CancelEditButton.IsVisible = true;
        FormMessage.Text = "Editing " + app.Id;
    }

    private void ResetForm(bool clearMessage = true)
    {
        _editingAppId = null;
        AppIdBox.IsEnabled = true;
        AppIdBox.Text = AppNameBox.Text = CommandBox.Text = EndpointBox.Text = string.Empty;
        CategoryBox.SelectedIndex = 0;
        FormTitle.Text = "Add application";
        AddButton.Content = "Add application";
        CancelEditButton.IsVisible = false;
        if (clearMessage) FormMessage.Text = string.Empty;
    }

    private void SaveConfigButton_Click(object? sender, RoutedEventArgs e)
    {
        _registry.UpdateZeroMqEndpoints(PubEndpointBox.Text ?? string.Empty, SubEndpointBox.Text ?? string.Empty);
        FormMessage.Text = "Configuration saved.";
    }
}
