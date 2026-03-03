using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;

namespace Nexplorer.App.Services;

public enum NotificationType { Info, Warning, Error, Success }

public sealed class NotificationItem
{
    public string Title { get; init; } = "";
    public string Message { get; init; } = "";
    public NotificationType Type { get; init; }
    internal DispatcherTimer? Timer { get; set; }
}

/// <summary>
/// Lightweight in-app toast notification service. Toasts auto-dismiss after a few seconds.
/// </summary>
public sealed class NotificationService
{
    public static NotificationService Instance { get; } = new();

    public ObservableCollection<NotificationItem> Notifications { get; } = new();

    public void Show(string message, string title = "", NotificationType type = NotificationType.Info, int durationMs = 4000)
    {
        var item = new NotificationItem { Title = title, Message = message, Type = type };

        Application.Current.Dispatcher.Invoke(() =>
        {
            Notifications.Add(item);

            if (durationMs > 0)
            {
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(durationMs) };
                timer.Tick += (_, _) => { timer.Stop(); Dismiss(item); };
                item.Timer = timer;
                timer.Start();
            }
        });
    }

    public void Dismiss(NotificationItem item)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            item.Timer?.Stop();
            Notifications.Remove(item);
        });
    }

    public void Info(string message, string title = "")    => Show(message, title, NotificationType.Info);
    public void Warn(string message, string title = "")    => Show(message, title, NotificationType.Warning, 5000);
    public void Error(string message, string title = "")   => Show(message, title, NotificationType.Error, 6000);
    public void Success(string message, string title = "") => Show(message, title, NotificationType.Success);

    /// <summary>
    /// Shows a styled confirmation dialog on the UI thread and returns the user's choice.
    /// </summary>
    public static bool Confirm(string message, string title = "Confirm")
    {
        return Application.Current.Dispatcher.Invoke(() =>
        {
            var dlg = new ConfirmDialog(title, message)
            {
                Owner = Application.Current.MainWindow
            };
            return dlg.ShowDialog() == true;
        });
    }
}
