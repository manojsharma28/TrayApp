using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using TrayApp.Shared.Interfaces;

namespace TrayApp.UI.Views;

public partial class TrayMenuWindow : Window
{
    private readonly IAppController _controller;
    private readonly IProcessManager _processManager;
    private readonly Dictionary<string, TextBlock> _statusLabels = new();
    private readonly Action _openSettings;
    private readonly Action _quit;

    public TrayMenuWindow(
        IAppRegistry registry,
        IAppController controller,
        IProcessManager processManager,
        Action openSettings,
        Action quit)
    {
        _controller = controller;
        _processManager = processManager;
        _openSettings = openSettings;
        _quit = quit;
        InitializeComponent();

        foreach (var app in registry.Apps.Where(app => !app.IsHidden && !string.IsNullOrWhiteSpace(app.Id) && !string.IsNullOrWhiteSpace(app.Name)))
        {
            AddAppItem(app);
        }

        if (!registry.Apps.Any(app => !app.IsHidden && !string.IsNullOrWhiteSpace(app.Id) && !string.IsNullOrWhiteSpace(app.Name)))
        {
            AppItemsPanel.Children.Add(new TextBlock
            {
                Text = "No managed applications configured",
                Foreground = (IBrush)Application.Current!.FindResource("SecondaryTextBrush")!,
                FontSize = 12,
                Margin = new Avalonia.Thickness(10, 12)
            });
        }

        SettingsButton.Click += (_, _) =>
        {
            Close();
            _openSettings();
        };
        ExitButton.Click += (_, _) =>
        {
            Close();
            _quit();
        };
        Deactivated += (_, _) => Close();
    }

    public void UpdateStatus(string appId, string status)
    {
        if (_statusLabels.TryGetValue(appId, out var label))
        {
            label.Text = status.ToUpperInvariant();
        }
    }

    private void AddAppItem(TrayApp.Shared.Models.AppInfo app)
    {
        if (string.IsNullOrWhiteSpace(app.Id) || string.IsNullOrWhiteSpace(app.Name))
        {
            return;
        }

        var status = new TextBlock
        {
            Text = "UNKNOWN",
            Foreground = (IBrush)Application.Current!.FindResource("SecondaryTextBrush")!,
            FontSize = 10,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        _statusLabels[app.Id] = status;

        var start = new Button
        {
            Content = MenuButtonText("Start"),
            Classes = { "menu-action" },
            HorizontalContentAlignment = HorizontalAlignment.Left
        };
        start.Click += async (_, _) =>
        {
            await _controller.StartAsync(app.Id);
            UpdateStatus(app.Id, "starting");
        };

        var stop = new Button
        {
            Content = MenuButtonText("Stop"),
            Classes = { "menu-action" },
            HorizontalContentAlignment = HorizontalAlignment.Left
        };
        stop.Click += async (_, _) =>
        {
            await _controller.StopAsync(app.Id);
            UpdateStatus(app.Id, "stopping");
        };

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            Children = { start, stop }
        };
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            Margin = new Avalonia.Thickness(6, 7)
        };
        row.Children.Add(new TextBlock
        {
            Text = app.Name,
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            Foreground = (IBrush)Application.Current!.FindResource("PrimaryTextBrush")!
        });
        Grid.SetColumn(status, 1);
        row.Children.Add(status);
        Grid.SetRow(actions, 1);
        Grid.SetColumnSpan(actions, 2);
        row.Children.Add(actions);
        AppItemsPanel.Children.Add(row);
    }

    private static TextBlock MenuButtonText(string text)
    {
        return new TextBlock
        {
            Text = text,
            Foreground = (IBrush)Application.Current!.FindResource("PrimaryTextBrush")!,
            FontSize = 13
        };
    }
}