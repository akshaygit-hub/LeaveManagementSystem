using Shared.Models;

namespace NotificationService.Repositories;

public class InMemoryNotificationRepository : INotificationRepository
{
    private static readonly List<Notification> _notifications = new();
    private static readonly object _lock = new();

    public Task AddAsync(Notification notification)
    {
        lock (_lock)
        {
            _notifications.Add(notification);
        }
        return Task.CompletedTask;
    }

    public Task<List<Notification>> GetByUserIdAsync(Guid userId)
    {
        lock (_lock)
        {
            var result = _notifications.Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt).ToList();
            return Task.FromResult(result);
        }
    }

    public Task<List<Notification>> GetUnreadByUserIdAsync(Guid userId)
    {
        lock (_lock)
        {
            var result = _notifications.Where(n => n.UserId == userId && !n.IsRead)
                .OrderByDescending(n => n.CreatedAt).ToList();
            return Task.FromResult(result);
        }
    }

    public Task<Notification?> GetByIdAsync(Guid id)
    {
        lock (_lock)
        {
            var notification = _notifications.FirstOrDefault(n => n.Id == id);
            return Task.FromResult(notification);
        }
    }

    public Task MarkAsReadAsync(Guid id)
    {
        lock (_lock)
        {
            var notification = _notifications.FirstOrDefault(n => n.Id == id);
            if (notification != null)
            {
                notification.IsRead = true;
            }
        }
        return Task.CompletedTask;
    }
}
