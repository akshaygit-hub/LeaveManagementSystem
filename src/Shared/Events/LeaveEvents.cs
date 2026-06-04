namespace Shared.Events;

public class LeaveAppliedEvent
{
    public Guid LeaveRequestId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public Guid ManagerId { get; set; }
    public string LeaveType { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int NumberOfDays { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class LeaveApprovedEvent
{
    public Guid LeaveRequestId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public Guid ManagerId { get; set; }
    public string LeaveType { get; set; } = string.Empty;
    public int NumberOfDays { get; set; }
}

public class LeaveRejectedEvent
{
    public Guid LeaveRequestId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public Guid ManagerId { get; set; }
    public string LeaveType { get; set; } = string.Empty;
    public string RejectionReason { get; set; } = string.Empty;
}
