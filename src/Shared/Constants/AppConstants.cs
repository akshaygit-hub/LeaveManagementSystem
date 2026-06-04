namespace Shared.Constants;

public static class LeaveConstants
{
    public const int DefaultCasualLeave = 12;
    public const int DefaultSickLeave = 10;
    public const int DefaultPrivilegeLeave = 15;
}

public static class RabbitMQConstants
{
    public const string LeaveAppliedQueue = "leave-applied-queue";
    public const string LeaveApprovedQueue = "leave-approved-queue";
    public const string LeaveRejectedQueue = "leave-rejected-queue";
    public const string ExchangeName = "leave-management-exchange";
}
