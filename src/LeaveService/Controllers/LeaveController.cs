using LeaveService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.DTOs;
using Shared.Exceptions;
using Shared.Models;

namespace LeaveService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LeaveController : ControllerBase
{
    private readonly ILeaveService _leaveService;
    private readonly ILogger<LeaveController> _logger;

    public LeaveController(ILeaveService leaveService, ILogger<LeaveController> logger)
    {
        _leaveService = leaveService;
        _logger = logger;
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

    private string GetCurrentUserRole()
    {
        return User.FindFirst("role")?.Value ?? string.Empty;
    }

    private string GetCurrentUserName()
    {
        return User.Identity?.Name ?? "Unknown";
    }

    /// <summary>
    /// Get leave balances for the current employee
    /// </summary>
    [HttpGet("balance")]
    public async Task<IActionResult> GetLeaveBalance()
    {
        var userId = GetCurrentUserId();
        var balances = await _leaveService.GetLeaveBalancesAsync(userId);
        return Ok(ApiResponse<List<LeaveBalanceResponse>>.SuccessResponse(balances));
    }

    /// <summary>
    /// Apply for leave
    /// </summary>
    [HttpPost("apply")]
    public async Task<IActionResult> ApplyLeave([FromBody] ApplyLeaveRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse.FailureResponse("Invalid request",
                ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)).ToList()));
        }

        var userId = GetCurrentUserId();
        var userName = GetCurrentUserName();
        var result = await _leaveService.ApplyLeaveAsync(userId, userName, request);

        return CreatedAtAction(nameof(GetLeaveRequestById), new { id = result.Id },
            ApiResponse<LeaveRequestResponse>.SuccessResponse(result, "Leave request submitted successfully"));
    }

    /// <summary>
    /// Get leave request by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetLeaveRequestById(Guid id)
    {
        var userId = GetCurrentUserId();
        var role = GetCurrentUserRole();

        var requests = await _leaveService.GetLeaveHistoryAsync(userId, new LeaveHistoryQuery { PageSize = int.MaxValue });
        var request = requests.Data.FirstOrDefault(r => r.Id == id);

        if (request == null && role == "Manager")
        {
            var teamRequests = await _leaveService.GetTeamLeaveRequestsAsync(userId, null, null, null, null, 1, int.MaxValue);
            request = teamRequests.Data.FirstOrDefault(r => r.Id == id);
        }

        if (request == null)
        {
            return NotFound(ApiResponse.FailureResponse("Leave request not found"));
        }

        return Ok(ApiResponse<LeaveRequestResponse>.SuccessResponse(request));
    }

    /// <summary>
    /// Get leave history for current employee
    /// </summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetLeaveHistory([FromQuery] LeaveStatus? status, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10, [FromQuery] DateTime? fromDate = null, [FromQuery] DateTime? toDate = null)
    {
        var userId = GetCurrentUserId();
        var query = new LeaveHistoryQuery
        {
            Status = status,
            Page = page,
            PageSize = pageSize,
            FromDate = fromDate,
            ToDate = toDate
        };

        var result = await _leaveService.GetLeaveHistoryAsync(userId, query);
        return Ok(ApiResponse<PaginatedResponse<LeaveRequestResponse>>.SuccessResponse(result));
    }

    /// <summary>
    /// Get team leave requests (Manager only)
    /// </summary>
    [HttpGet("team-requests")]
    [Authorize(Roles = "Manager")]
    public async Task<IActionResult> GetTeamLeaveRequests([FromQuery] LeaveStatus? status, [FromQuery] Guid? employeeId,
        [FromQuery] DateTime? fromDate = null, [FromQuery] DateTime? toDate = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var managerId = GetCurrentUserId();
        var result = await _leaveService.GetTeamLeaveRequestsAsync(managerId, status, employeeId, fromDate, toDate, page, pageSize);
        return Ok(ApiResponse<PaginatedResponse<LeaveRequestResponse>>.SuccessResponse(result));
    }

    /// <summary>
    /// Approve a leave request (Manager only)
    /// </summary>
    [HttpPut("{id}/approve")]
    [Authorize(Roles = "Manager")]
    public async Task<IActionResult> ApproveLeave(Guid id)
    {
        var managerId = GetCurrentUserId();
        var result = await _leaveService.ApproveLeaveAsync(id, managerId);
        return Ok(ApiResponse<LeaveRequestResponse>.SuccessResponse(result, "Leave request approved successfully"));
    }

    /// <summary>
    /// Reject a leave request (Manager only)
    /// </summary>
    [HttpPut("{id}/reject")]
    [Authorize(Roles = "Manager")]
    public async Task<IActionResult> RejectLeave(Guid id, [FromBody] LeaveActionRequest request)
    {
        var managerId = GetCurrentUserId();
        var result = await _leaveService.RejectLeaveAsync(id, managerId, request.Comments);
        return Ok(ApiResponse<LeaveRequestResponse>.SuccessResponse(result, "Leave request rejected"));
    }

    /// <summary>
    /// Cancel a leave request (Employee only - own requests)
    /// </summary>
    [HttpPut("{id}/cancel")]
    public async Task<IActionResult> CancelLeave(Guid id)
    {
        var employeeId = GetCurrentUserId();
        var result = await _leaveService.CancelLeaveAsync(id, employeeId);
        return Ok(ApiResponse<LeaveRequestResponse>.SuccessResponse(result, "Leave request cancelled"));
    }
}
