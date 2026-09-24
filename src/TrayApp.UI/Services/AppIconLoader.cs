using Avalonia.Controls;

namespace TrayApp.UI.Services;

public static class AppIconLoader
{
    public static string? FindPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Assets", "trayapp-icon.png"),
            Path.Combine(AppContext.BaseDirectory, "trayapp-icon.png"),
            Path.Combine(Directory.GetCurrentDirectory(), "Assets", "trayapp-icon.png")
        };

        return candidates
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(File.Exists);
    }

    public static WindowIcon? Load()
    {
        var path = FindPath();
        if (path != null)
        {
            return new WindowIcon(path);
        }

        return null;
    }
}
