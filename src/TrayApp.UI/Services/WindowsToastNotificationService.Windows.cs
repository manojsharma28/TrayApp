using DrawingBitmap = System.Drawing.Bitmap;
using DrawingIcon = System.Drawing.Icon;
using System.Runtime.InteropServices;
using FormsNotifyIcon = System.Windows.Forms.NotifyIcon;
using FormsToolTipIcon = System.Windows.Forms.ToolTipIcon;

namespace TrayApp.UI.Services;

public sealed class WindowsToastNotificationService
{
    public async void ShowApplicationStopped(string applicationName)
    {
        FormsNotifyIcon? notifyIcon = null;
        DrawingIcon? icon = null;
        try
        {
            var iconPath = AppIconLoader.FindPath();
            if (iconPath != null)
            {
                icon = LoadIconFromPng(iconPath);
            }

            notifyIcon = new NotifyIcon
            {
                Icon = icon,
                Visible = true,
                BalloonTipTitle = "AppHive",
                BalloonTipText = $"{applicationName} has stopped running.",
                BalloonTipIcon = FormsToolTipIcon.Info
            };
            notifyIcon.ShowBalloonTip(5000);
            await Task.Delay(TimeSpan.FromSeconds(6));
        }
        catch
        {
            // Notifications are best effort and must not affect process monitoring.
        }
        finally
        {
            notifyIcon?.Dispose();
            icon?.Dispose();
        }
    }

    private static DrawingIcon LoadIconFromPng(string path)
    {
        using var bitmap = new DrawingBitmap(path);
        var handle = bitmap.GetHicon();
        try
        {
            using var source = DrawingIcon.FromHandle(handle);
            return (DrawingIcon)source.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);
}
