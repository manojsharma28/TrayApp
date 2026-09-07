using Avalonia.Controls;
using Avalonia.Interactivity;
using TrayApp.Shared.Interfaces;

namespace TrayApp.UI.Views;

public partial class SettingsWindow : Window
{
    private readonly IAppRegistry _registry;

    public SettingsWindow()
        : this(new TrayApp.Shared.Services.AppRegistryService())
    {
    }

    public SettingsWindow(IAppRegistry registry)
    {
        _registry = registry;
        InitializeComponent();

        var pub = this.FindControl<TextBox>("PubEndpoint");
        var sub = this.FindControl<TextBox>("SubEndpoint");

        if (pub != null)
            pub.Text = _registry.PubEndpoint;
        if (sub != null)
            sub.Text = _registry.SubEndpoint;

        var btn = this.FindControl<Button>("SaveButton");
        if (btn != null)
            btn.Click += SaveButton_Click;
    }

    private void SaveButton_Click(object? sender, RoutedEventArgs e)
    {
        var pub = this.FindControl<TextBox>("PubEndpoint");
        var sub = this.FindControl<TextBox>("SubEndpoint");

        if (pub != null && sub != null)
        {
            _registry.UpdateZeroMqEndpoints(pub.Text ?? string.Empty, sub.Text ?? string.Empty);
        }

        Close();
    }
}
