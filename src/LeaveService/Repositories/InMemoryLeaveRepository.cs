using Shared.Constants;
using Shared.Models;

namespace LeaveService.Repositories;

public class InMemoryLeaveRepository : ILeaveRepository
{
    private static readonly List<LeaveRequest> _leaveRequests = new();
    private static readonly List<LeaveBalance> _leaveBalances = new();
    private static bool _seeded = false;
    private static readonly object _lock = new();

    public InMemoryLeaveRepository()
    {
        lock (_lock)
        {
            if (!_seeded)
            {
                SeedData();
                _seeded = true;
            }
        }
    }

    private static void SeedData()
    {
        var employeeIds = new[]
        {
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Guid.Parse("44444444-4444-4444-4444-444444444444"),
            Guid.Parse("55555555-5555-5555-5555-555555555555")
        };

        foreach (var empId in employeeIds)
        {
            _leaveBalances.Add(new LeaveBalance
            {
                EmployeeId = empId,
                LeaveType = LeaveType.Casual,
                TotalAllocated = LeaveConstants.DefaultCasualLeave,
                Used = 0
            });
            _leaveBalances.Add(new LeaveBalance
            {
                EmployeeId = empId,
                LeaveType = LeaveType.Sick,
                TotalAllocated = LeaveConstants.DefaultSickLeave,
                Used = 0
            });
            _leaveBalances.Add(new LeaveBalance
            {
                EmployeeId = empId,
                LeaveType = LeaveType.Privilege,
                TotalAllocated = LeaveConstants.DefaultPrivilegeLeave,
                Used = 0
            });
        }
    }

    public Task<List<LeaveBalance>> GetLeaveBalancesAsync(Guid employeeId)
    {
        var balances = _leaveBalances.Where(lb => lb.EmployeeId == employeeId).ToList();
        return Task.FromResult(balances);
    }

    public Task<LeaveBalance?> GetLeaveBalanceAsync(Guid employeeId, LeaveType leaveType)
    {
        var balance = _leaveBalances.FirstOrDefault(lb => lb.EmployeeId == employeeId && lb.LeaveType == leaveType);
        return Task.FromResult(balance);
    }

    public Task DeductLeaveAsync(Guid employeeId, LeaveType leaveType, int days)
    {
        var balance = _leaveBalances.FirstOrDefault(lb => lb.EmployeeId == employeeId && lb.LeaveType == leaveType);
        if (balance != null)
        {
            balance.Used += days;
        }
        return Task.CompletedTask;
    }

    public Task RestoreLeaveAsync(Guid employeeId, LeaveType leaveType, int days)
    {
        var balance = _leaveBalances.FirstOrDefault(lb => lb.EmployeeId == employeeId && lb.LeaveType == leaveType);
        if (balance != null)
        {
            balance.Used = Math.Max(0, balance.Used - days);
        }
        return Task.CompletedTask;
    }

    public Task<LeaveRequest> AddLeaveRequestAsync(LeaveRequest request)
    {
        _leaveRequests.Add(request);
        return Task.FromResult(request);
    }

    public Task<LeaveRequest?> GetLeaveRequestByIdAsync(Guid id)
    {
        var request = _leaveRequests.FirstOrDefault(lr => lr.Id == id);
        return Task.FromResult(request);
    }

    public Task<List<LeaveRequest>> GetLeaveRequestsByEmployeeAsync(Guid employeeId)
    {
        var requests = _leaveRequests.Where(lr => lr.EmployeeId == employeeId)
            .OrderByDescending(lr => lr.CreatedAt).ToList();
        return Task.FromResult(requests);
    }

    public Task<List<LeaveRequest>> GetLeaveRequestsByManagerAsync(Guid managerId)
    {
        var requests = _leaveRequests.Where(lr => lr.ManagerId == managerId)
            .OrderByDescending(lr => lr.CreatedAt).ToList();
        return Task.FromResult(requests);
    }

    public Task<bool> HasOverlappingLeaveAsync(Guid employeeId, DateTime startDate, DateTime endDate, Guid? excludeId = null)
    {
        var hasOverlap = _leaveRequests.Any(lr =>
            lr.EmployeeId == employeeId &&
            lr.Status != LeaveStatus.Rejected &&
            lr.Status != LeaveStatus.Cancelled &&
            (excludeId == null || lr.Id != excludeId) &&
            lr.StartDate <= endDate &&
            lr.EndDate >= startDate);
        return Task.FromResult(hasOverlap);
    }

    public Task UpdateLeaveRequestAsync(LeaveRequest request)
    {
        // In-memory objects are updated by reference, no-op needed
        return Task.CompletedTask;
    }
}
