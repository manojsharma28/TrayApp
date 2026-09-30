using Avalonia;
using Avalonia.Media;

namespace TrayApp.UI.Services;

public static class UiThemeService
{
    public static void Apply(string? theme)
    {
        var isBlue = string.Equals(theme, "blue", StringComparison.OrdinalIgnoreCase);
        var resources = Application.Current?.Resources;
        if (resources == null)
        {
            return;
        }

        resources["AccentBrush"] = new SolidColorBrush(Color.Parse(isBlue ? "#35679F" : "#52735F"));
        resources["AccentHoverBrush"] = new SolidColorBrush(Color.Parse(isBlue ? "#294F7E" : "#3F604C"));
        resources["ActionButtonBrush"] = new SolidColorBrush(Color.Parse(isBlue ? "#E4ECF5" : "#E8ECE5"));
        resources["ActionHoverBrush"] = new SolidColorBrush(Color.Parse(isBlue ? "#35679F" : "#557560"));
        resources["ActionPressedBrush"] = new SolidColorBrush(Color.Parse(isBlue ? "#294F7E" : "#3F604C"));
        resources["ActionBorderBrush"] = new SolidColorBrush(Color.Parse(isBlue ? "#B6C8DC" : "#BCC8BC"));
        resources["RunningStatusBrush"] = new SolidColorBrush(Color.Parse("#2F7548"));
        resources["StoppedStatusBrush"] = new SolidColorBrush(Color.Parse("#A9433E"));
    }
}
