using Shared.Models;

namespace NotificationService.Repositories;

public interface INotificationRepository
{
    Task AddAsync(Notification notification);
    Task<List<Notification>> GetByUserIdAsync(Guid userId);
    Task<List<Notification>> GetUnreadByUserIdAsync(Guid userId);
    Task<Notification?> GetByIdAsync(Guid id);
    Task MarkAsReadAsync(Guid id);
}
