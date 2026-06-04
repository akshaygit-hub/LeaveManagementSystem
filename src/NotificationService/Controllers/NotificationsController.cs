using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NotificationService.Repositories;
using Shared.DTOs;
using Shared.Exceptions;

namespace NotificationService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly ILogger<NotificationsController> _logger;
    private readonly INotificationRepository _notificationRepository;

    public NotificationsController(ILogger<NotificationsController> logger, INotificationRepository notificationRepository)
    {
        _logger = logger;
        _notificationRepository = notificationRepository;
    }

    private Guid GetCurrentUserId()
    {
        var userId = User.FindFirst("userId")?.Value;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var id))
        {
            throw new UnauthorizedException("Invalid token");
        }
        return id;
    }

    [HttpGet]
    public async Task<IActionResult> GetNotifications()
    {
        var userId = GetCurrentUserId();
        var notifications = await _notificationRepository.GetByUserIdAsync(userId);
        var result = notifications.Select(n => new
        {
            n.Id,
            n.Message,
            Type = n.Type.ToString(),
            n.IsRead,
            n.CreatedAt
        });
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("unread")]
    public async Task<IActionResult> GetUnreadNotifications()
    {
        var userId = GetCurrentUserId();
        var notifications = await _notificationRepository.GetUnreadByUserIdAsync(userId);
        var result = notifications.Select(n => new
        {
            n.Id,
            n.Message,
            Type = n.Type.ToString(),
            n.IsRead,
            n.CreatedAt
        });
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpPut("{id}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        var userId = GetCurrentUserId();
        var notification = await _notificationRepository.GetByIdAsync(id);
        if (notification == null)
        {
            return NotFound(ApiResponse.FailureResponse("Notification not found"));
        }

        if (notification.UserId != userId)
        {
            return Forbid();
        }

        await _notificationRepository.MarkAsReadAsync(id);
        return Ok(ApiResponse.SuccessResponse("Notification marked as read"));
    }
}
