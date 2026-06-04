namespace Shared.Configuration;

public class RabbitMQSettings
{
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";
    public string ExchangeName { get; set; } = "leave-management-exchange";

    // Message TTL: Messages expire after this duration (in milliseconds)
    // Default: 24 hours (86400000 ms) - prevents old messages from being processed after long downtime
    public int MessageTtlMilliseconds { get; set; } = 86400000;
}
