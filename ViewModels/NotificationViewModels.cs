using Pressing.Models;

namespace Pressing.ViewModels;

public sealed class NotificationListViewModel
{
    public IReadOnlyList<UserNotification> Notifications { get; init; } = [];
    public int UnreadCount { get; init; }
}
