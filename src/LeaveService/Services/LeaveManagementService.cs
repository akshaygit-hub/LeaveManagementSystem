using LeaveService.Repositories;
using Shared.Constants;
using Shared.DTOs;
using Shared.Events;
using Shared.Exceptions;
using Shared.Models;

namespace LeaveService.Services;

public interface ILeaveService
{
    Task<List<LeaveBalanceResponse>> GetLeaveBalancesAsync(Guid employeeId);
    Task<LeaveRequestResponse> ApplyLeaveAsync(Guid employeeId, string employeeName, ApplyLeaveRequest request);
    Task<PaginatedResponse<LeaveRequestResponse>> GetLeaveHistoryAsync(Guid employeeId, LeaveHistoryQuery query);
    Task<PaginatedResponse<LeaveRequestResponse>> GetTeamLeaveRequestsAsync(Guid managerId, LeaveStatus? status, Guid? employeeId, DateTime? fromDate, DateTime? toDate, int page, int pageSize);
    Task<LeaveRequestResponse> ApproveLeaveAsync(Guid leaveRequestId, Guid managerId);
    Task<LeaveRequestResponse> RejectLeaveAsync(Guid leaveRequestId, Guid managerId, string? reason);
    Task<LeaveRequestResponse> CancelLeaveAsync(Guid leaveRequestId, Guid employeeId);
}

public class LeaveManagementService : ILeaveService
{
    private readonly IMessagePublisher _publisher;
    private readonly ILogger<LeaveManagementService> _logger;
    private readonly ILeaveRepository _leaveRepository;

    public LeaveManagementService(IMessagePublisher publisher, ILogger<LeaveManagementService> logger, ILeaveRepository leaveRepository)
    {
        _publisher = publisher;
        _logger = logger;
        _leaveRepository = leaveRepository;
    }

    public async Task<List<LeaveBalanceResponse>> GetLeaveBalancesAsync(Guid employeeId)
    {
        var balances = await _leaveRepository.GetLeaveBalancesAsync(employeeId);
        if (!balances.Any())
        {
            throw new NotFoundException($"No leave balances found for employee {employeeId}");
        }

        return balances.Select(b => new LeaveBalanceResponse
        {
            LeaveType = b.LeaveType,
            LeaveTypeName = b.LeaveType.ToString(),
            TotalAllocated = b.TotalAllocated,
            Used = b.Used,
            Remaining = b.Remaining
        }).ToList();
    }

    public async Task<LeaveRequestResponse> ApplyLeaveAsync(Guid employeeId, string employeeName, ApplyLeaveRequest request)
    {
        // Validate dates
        if (request.StartDate.Date < DateTime.UtcNow.Date)
        {
            throw new BusinessException("Start date cannot be in the past");
        }

        if (request.EndDate.Date < request.StartDate.Date)
        {
            throw new BusinessException("End date cannot be before start date");
        }

        // Validate leave balance
        var balance = await _leaveRepository.GetLeaveBalanceAsync(employeeId, request.LeaveType);
        if (balance == null)
        {
            throw new NotFoundException("Leave balance not found");
        }

        if (balance.Remaining < request.NumberOfDays)
        {
            throw new BusinessException($"Insufficient leave balance. Available: {balance.Remaining}, Requested: {request.NumberOfDays}");
        }

        // Check overlapping leaves
        if (await _leaveRepository.HasOverlappingLeaveAsync(employeeId, request.StartDate, request.EndDate))
        {
            throw new ConflictException("You already have a leave request overlapping with the specified dates");
        }

        // Create leave request
        var leaveRequest = new LeaveRequest
        {
            EmployeeId = employeeId,
            EmployeeName = employeeName,
            LeaveType = request.LeaveType,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            NumberOfDays = request.NumberOfDays,
            Reason = request.Reason,
            ManagerId = request.ManagerId,
            Status = LeaveStatus.Pending
        };

        await _leaveRepository.AddLeaveRequestAsync(leaveRequest);
        _logger.LogInformation("Leave request {Id} created for employee {EmployeeId}", leaveRequest.Id, employeeId);

        // Publish event
        var leaveEvent = new LeaveAppliedEvent
        {
            LeaveRequestId = leaveRequest.Id,
            EmployeeId = employeeId,
            EmployeeName = employeeName,
            ManagerId = request.ManagerId,
            LeaveType = request.LeaveType.ToString(),
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            NumberOfDays = request.NumberOfDays,
            Reason = request.Reason
        };
        await _publisher.PublishAsync(RabbitMQConstants.LeaveAppliedQueue, leaveEvent);

        return MapToResponse(leaveRequest);
    }

    public async Task<PaginatedResponse<LeaveRequestResponse>> GetLeaveHistoryAsync(Guid employeeId, LeaveHistoryQuery query)
    {
        var requests = await _leaveRepository.GetLeaveRequestsByEmployeeAsync(employeeId);

        if (query.Status.HasValue)
        {
            requests = requests.Where(r => r.Status == query.Status.Value).ToList();
        }

        if (query.FromDate.HasValue)
        {
            requests = requests.Where(r => r.StartDate >= query.FromDate.Value).ToList();
        }

        if (query.ToDate.HasValue)
        {
            requests = requests.Where(r => r.EndDate <= query.ToDate.Value).ToList();
        }

        var totalCount = requests.Count;
        var paginatedData = requests
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(MapToResponse)
            .ToList();

        return new PaginatedResponse<LeaveRequestResponse>
        {
            Data = paginatedData,
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    public async Task<PaginatedResponse<LeaveRequestResponse>> GetTeamLeaveRequestsAsync(Guid managerId, LeaveStatus? status, Guid? employeeId, DateTime? fromDate, DateTime? toDate, int page, int pageSize)
    {
        var requests = await _leaveRepository.GetLeaveRequestsByManagerAsync(managerId);

        if (status.HasValue)
        {
            requests = requests.Where(r => r.Status == status.Value).ToList();
        }

        if (employeeId.HasValue)
        {
            requests = requests.Where(r => r.EmployeeId == employeeId.Value).ToList();
        }

        if (fromDate.HasValue)
        {
            requests = requests.Where(r => r.StartDate >= fromDate.Value).ToList();
        }

        if (toDate.HasValue)
        {
            requests = requests.Where(r => r.EndDate <= toDate.Value).ToList();
        }

        var totalCount = requests.Count;
        var paginatedData = requests
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(MapToResponse)
            .ToList();

        return new PaginatedResponse<LeaveRequestResponse>
        {
            Data = paginatedData,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<LeaveRequestResponse> ApproveLeaveAsync(Guid leaveRequestId, Guid managerId)
    {
        var request = await _leaveRepository.GetLeaveRequestByIdAsync(leaveRequestId);
        if (request == null)
        {
            throw new NotFoundException("Leave request not found");
        }

        if (request.ManagerId != managerId)
        {
            throw new ForbiddenException("You are not authorized to approve this leave request");
        }

        if (request.Status != LeaveStatus.Pending)
        {
            throw new BusinessException($"Leave request is already {request.Status}");
        }

        // Deduct leave balance
        await _leaveRepository.DeductLeaveAsync(request.EmployeeId, request.LeaveType, request.NumberOfDays);

        request.Status = LeaveStatus.Approved;
        request.UpdatedAt = DateTime.UtcNow;
        await _leaveRepository.UpdateLeaveRequestAsync(request);

        _logger.LogInformation("Leave request {Id} approved by manager {ManagerId}", leaveRequestId, managerId);

        // Publish event
        var approvedEvent = new LeaveApprovedEvent
        {
            LeaveRequestId = request.Id,
            EmployeeId = request.EmployeeId,
            EmployeeName = request.EmployeeName,
            ManagerId = managerId,
            LeaveType = request.LeaveType.ToString(),
            NumberOfDays = request.NumberOfDays
        };
        await _publisher.PublishAsync(RabbitMQConstants.LeaveApprovedQueue, approvedEvent);

        return MapToResponse(request);
    }

    public async Task<LeaveRequestResponse> RejectLeaveAsync(Guid leaveRequestId, Guid managerId, string? reason)
    {
        var request = await _leaveRepository.GetLeaveRequestByIdAsync(leaveRequestId);
        if (request == null)
        {
            throw new NotFoundException("Leave request not found");
        }

        if (request.ManagerId != managerId)
        {
            throw new ForbiddenException("You are not authorized to reject this leave request");
        }

        if (request.Status != LeaveStatus.Pending)
        {
            throw new BusinessException($"Leave request is already {request.Status}");
        }

        request.Status = LeaveStatus.Rejected;
        request.RejectionReason = reason;
        request.UpdatedAt = DateTime.UtcNow;
        await _leaveRepository.UpdateLeaveRequestAsync(request);

        _logger.LogInformation("Leave request {Id} rejected by manager {ManagerId}", leaveRequestId, managerId);

        // Publish event
        var rejectedEvent = new LeaveRejectedEvent
        {
            LeaveRequestId = request.Id,
            EmployeeId = request.EmployeeId,
            EmployeeName = request.EmployeeName,
            ManagerId = managerId,
            LeaveType = request.LeaveType.ToString(),
            RejectionReason = reason ?? "No reason provided"
        };
        await _publisher.PublishAsync(RabbitMQConstants.LeaveRejectedQueue, rejectedEvent);

        return MapToResponse(request);
    }

    public async Task<LeaveRequestResponse> CancelLeaveAsync(Guid leaveRequestId, Guid employeeId)
    {
        var request = await _leaveRepository.GetLeaveRequestByIdAsync(leaveRequestId);
        if (request == null)
        {
            throw new NotFoundException("Leave request not found");
        }

        if (request.EmployeeId != employeeId)
        {
            throw new ForbiddenException("You can only cancel your own leave requests");
        }

        if (request.Status != LeaveStatus.Pending)
        {
            throw new BusinessException($"Only pending leave requests can be cancelled. Current status: {request.Status}");
        }

        request.Status = LeaveStatus.Cancelled;
        request.UpdatedAt = DateTime.UtcNow;
        await _leaveRepository.UpdateLeaveRequestAsync(request);

        _logger.LogInformation("Leave request {Id} cancelled by employee {EmployeeId}", leaveRequestId, employeeId);
        return MapToResponse(request);
    }

    private static LeaveRequestResponse MapToResponse(LeaveRequest request)
    {
        return new LeaveRequestResponse
        {
            Id = request.Id,
            EmployeeId = request.EmployeeId,
            EmployeeName = request.EmployeeName,
            LeaveType = request.LeaveType.ToString(),
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            NumberOfDays = request.NumberOfDays,
            Reason = request.Reason,
            Status = request.Status.ToString(),
            RejectionReason = request.RejectionReason,
            CreatedAt = request.CreatedAt,
            UpdatedAt = request.UpdatedAt
        };
    }
}
