using System.Drawing.Imaging;
using TrayApp.Shared.Models;
using AvaloniaBitmap = Avalonia.Media.Imaging.Bitmap;

namespace TrayApp.UI.Services;

public static class AppExecutableIconLoader
{
    public static AvaloniaBitmap? Load(AppInfo app)
    {
        var executablePath = ResolveExecutablePath(app);
        if (executablePath == null)
        {
            return null;
        }

#if WINDOWS
        try
        {
            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(executablePath);
            if (icon == null)
            {
                return null;
            }

            using var image = icon.ToBitmap();
            using var stream = new MemoryStream();
            image.Save(stream, ImageFormat.Png);
            stream.Position = 0;
            return new AvaloniaBitmap(stream);
        }
        catch
        {
            return null;
        }
#else
        return null;
#endif
    }

    private static string? ResolveExecutablePath(AppInfo app)
    {
        if (!string.IsNullOrWhiteSpace(app.ExecutablePath))
        {
            return File.Exists(app.ExecutablePath) ? app.ExecutablePath : null;
        }

        var command = app.StartCommand?.Trim();
        if (string.IsNullOrEmpty(command))
        {
            return null;
        }

        if (command[0] == '\"')
        {
            var closingQuote = command.IndexOf('\"', 1);
            if (closingQuote > 1)
            {
                var quotedPath = command[1..closingQuote];
                return File.Exists(quotedPath) ? quotedPath : null;
            }
        }

        if (File.Exists(command))
        {
            return command;
        }

        var exeExtension = command.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (exeExtension >= 0)
        {
            var executablePath = command[..(exeExtension + 4)].Trim(' ', '\"');
            return File.Exists(executablePath) ? executablePath : null;
        }

        var firstToken = command.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries)[0].Trim(' ', '\"');
        return File.Exists(firstToken) ? firstToken : null;
    }
}