using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using NotificationService.Repositories;
using Shared.Configuration;
using Shared.Constants;
using Shared.Events;
using Shared.Models;

namespace NotificationService.Services;

public class RabbitMQConsumerService : BackgroundService
{
    private readonly ILogger<RabbitMQConsumerService> _logger;
    private readonly RabbitMQSettings _settings;
    private readonly IServiceScopeFactory _scopeFactory;
    private IConnection? _connection;
    private IChannel? _channel;

    public RabbitMQConsumerService(IOptions<RabbitMQSettings> settings, ILogger<RabbitMQConsumerService> logger, IServiceScopeFactory scopeFactory)
    {
        _settings = settings.Value;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    private INotificationRepository GetRepository()
    {
        var scope = _scopeFactory.CreateScope();
        return scope.ServiceProvider.GetRequiredService<INotificationRepository>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var factory = new ConnectionFactory
            {
                HostName = _settings.HostName,
                Port = _settings.Port,
                UserName = _settings.UserName,
                Password = _settings.Password
            };

            _connection = await factory.CreateConnectionAsync(stoppingToken);
            _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

            // Declare queues
            await _channel.QueueDeclareAsync(RabbitMQConstants.LeaveAppliedQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);
            await _channel.QueueDeclareAsync(RabbitMQConstants.LeaveApprovedQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);
            await _channel.QueueDeclareAsync(RabbitMQConstants.LeaveRejectedQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);

            // Consumer for Leave Applied
            var appliedConsumer = new AsyncEventingBasicConsumer(_channel);
            appliedConsumer.ReceivedAsync += async (model, ea) =>
            {
                var body = Encoding.UTF8.GetString(ea.Body.ToArray());
                _logger.LogInformation("[NOTIFICATION] Leave Applied Event received: {Body}", body);

                var leaveEvent = JsonSerializer.Deserialize<LeaveAppliedEvent>(body);
                if (leaveEvent != null)
                {
                    var repository = GetRepository();

                    // Notification for employee
                    await repository.AddAsync(new Notification
                    {
                        UserId = leaveEvent.EmployeeId,
                        Message = $"Your {leaveEvent.LeaveType} leave request from {leaveEvent.StartDate:yyyy-MM-dd} to {leaveEvent.EndDate:yyyy-MM-dd} has been submitted successfully.",
                        Type = NotificationType.LeaveApplied
                    });

                    // Notification for manager
                    await repository.AddAsync(new Notification
                    {
                        UserId = leaveEvent.ManagerId,
                        Message = $"{leaveEvent.EmployeeName} has applied for {leaveEvent.LeaveType} leave from {leaveEvent.StartDate:yyyy-MM-dd} to {leaveEvent.EndDate:yyyy-MM-dd} ({leaveEvent.NumberOfDays} days). Reason: {leaveEvent.Reason}",
                        Type = NotificationType.LeaveApplied
                    });

                    _logger.LogInformation("[NOTIFICATION] Notifications created for leave application {Id}", leaveEvent.LeaveRequestId);
                }

                await _channel.BasicAckAsync(ea.DeliveryTag, false);
            };

            // Consumer for Leave Approved
            var approvedConsumer = new AsyncEventingBasicConsumer(_channel);
            approvedConsumer.ReceivedAsync += async (model, ea) =>
            {
                var body = Encoding.UTF8.GetString(ea.Body.ToArray());
                _logger.LogInformation("[NOTIFICATION] Leave Approved Event received: {Body}", body);

                var leaveEvent = JsonSerializer.Deserialize<LeaveApprovedEvent>(body);
                if (leaveEvent != null)
                {
                    var repository = GetRepository();
                    await repository.AddAsync(new Notification
                    {
                        UserId = leaveEvent.EmployeeId,
                        Message = $"Your {leaveEvent.LeaveType} leave request has been approved. {leaveEvent.NumberOfDays} day(s) deducted from your balance.",
                        Type = NotificationType.LeaveApproved
                    });

                    _logger.LogInformation("[NOTIFICATION] Approval notification created for employee {EmployeeId}", leaveEvent.EmployeeId);
                }

                await _channel.BasicAckAsync(ea.DeliveryTag, false);
            };

            // Consumer for Leave Rejected
            var rejectedConsumer = new AsyncEventingBasicConsumer(_channel);
            rejectedConsumer.ReceivedAsync += async (model, ea) =>
            {
                var body = Encoding.UTF8.GetString(ea.Body.ToArray());
                _logger.LogInformation("[NOTIFICATION] Leave Rejected Event received: {Body}", body);

                var leaveEvent = JsonSerializer.Deserialize<LeaveRejectedEvent>(body);
                if (leaveEvent != null)
                {
                    var repository = GetRepository();
                    await repository.AddAsync(new Notification
                    {
                        UserId = leaveEvent.EmployeeId,
                        Message = $"Your {leaveEvent.LeaveType} leave request has been rejected. Reason: {leaveEvent.RejectionReason}",
                        Type = NotificationType.LeaveRejected
                    });

                    _logger.LogInformation("[NOTIFICATION] Rejection notification created for employee {EmployeeId}", leaveEvent.EmployeeId);
                }

                await _channel.BasicAckAsync(ea.DeliveryTag, false);
            };

            await _channel.BasicConsumeAsync(RabbitMQConstants.LeaveAppliedQueue, autoAck: false, consumer: appliedConsumer, cancellationToken: stoppingToken);
            await _channel.BasicConsumeAsync(RabbitMQConstants.LeaveApprovedQueue, autoAck: false, consumer: approvedConsumer, cancellationToken: stoppingToken);
            await _channel.BasicConsumeAsync(RabbitMQConstants.LeaveRejectedQueue, autoAck: false, consumer: rejectedConsumer, cancellationToken: stoppingToken);

            _logger.LogInformation("RabbitMQ consumers started successfully");

            // Keep the service running
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not connect to RabbitMQ. Notification consumer will not process messages. Service will still run for API access.");
        }
    }

    public override void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        base.Dispose();
    }
}
