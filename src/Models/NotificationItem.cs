using CommunityToolkit.Mvvm.ComponentModel;

namespace StreamTalkerClient.Models;

public enum NotificationSeverity
{
    Info,
    Success,
    Warning,
    Danger
}

public partial class NotificationItem : ObservableObject
{
    private static long _idCounter;

    public long Id { get; }
    public string Message { get; set; } = "";
    public NotificationSeverity Severity { get; set; } = NotificationSeverity.Info;
    public int DurationMs { get; set; } = 5000;
    public bool IsCloseable { get; set; } = true;

    [ObservableProperty]
    private bool _isRemoving;

    public NotificationItem()
    {
        Id = Interlocked.Increment(ref _idCounter);
    }
}
