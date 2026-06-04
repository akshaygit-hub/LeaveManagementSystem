namespace Shared.Models;

public class LeaveBalance
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EmployeeId { get; set; }
    public LeaveType LeaveType { get; set; }
    public int TotalAllocated { get; set; }
    public int Used { get; set; }
    public int Remaining => TotalAllocated - Used;
}
