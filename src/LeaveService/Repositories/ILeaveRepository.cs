using Shared.Models;

namespace LeaveService.Repositories;

public interface ILeaveRepository
{
    Task<List<LeaveBalance>> GetLeaveBalancesAsync(Guid employeeId);
    Task<LeaveBalance?> GetLeaveBalanceAsync(Guid employeeId, LeaveType leaveType);
    Task DeductLeaveAsync(Guid employeeId, LeaveType leaveType, int days);
    Task RestoreLeaveAsync(Guid employeeId, LeaveType leaveType, int days);
    Task<LeaveRequest> AddLeaveRequestAsync(LeaveRequest request);
    Task<LeaveRequest?> GetLeaveRequestByIdAsync(Guid id);
    Task<List<LeaveRequest>> GetLeaveRequestsByEmployeeAsync(Guid employeeId);
    Task<List<LeaveRequest>> GetLeaveRequestsByManagerAsync(Guid managerId);
    Task<bool> HasOverlappingLeaveAsync(Guid employeeId, DateTime startDate, DateTime endDate, Guid? excludeId = null);
    Task UpdateLeaveRequestAsync(LeaveRequest request);
}
