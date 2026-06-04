using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Shared.Configuration;
using Shared.Constants;

namespace LeaveService.Services;

public interface IMessagePublisher
{
    Task PublishAsync<T>(string queueName, T message);
}

public class RabbitMQPublisher : IMessagePublisher, IDisposable
{
    private readonly IConnection? _connection;
    private readonly IChannel? _channel;
    private readonly ILogger<RabbitMQPublisher> _logger;
    private readonly bool _isConnected;

    public RabbitMQPublisher(IOptions<RabbitMQSettings> settings, ILogger<RabbitMQPublisher> logger)
    {
        _logger = logger;
        try
        {
            var rabbitSettings = settings.Value;
            var factory = new ConnectionFactory
            {
                HostName = rabbitSettings.HostName,
                Port = rabbitSettings.Port,
                UserName = rabbitSettings.UserName,
                Password = rabbitSettings.Password
            };

            _connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
            _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();

            // Declare exchange and queues
            _channel.ExchangeDeclareAsync(RabbitMQConstants.ExchangeName, ExchangeType.Direct, durable: true).GetAwaiter().GetResult();
            _channel.QueueDeclareAsync(RabbitMQConstants.LeaveAppliedQueue, durable: true, exclusive: false, autoDelete: false).GetAwaiter().GetResult();
            _channel.QueueDeclareAsync(RabbitMQConstants.LeaveApprovedQueue, durable: true, exclusive: false, autoDelete: false).GetAwaiter().GetResult();
            _channel.QueueDeclareAsync(RabbitMQConstants.LeaveRejectedQueue, durable: true, exclusive: false, autoDelete: false).GetAwaiter().GetResult();

            _channel.QueueBindAsync(RabbitMQConstants.LeaveAppliedQueue, RabbitMQConstants.ExchangeName, RabbitMQConstants.LeaveAppliedQueue).GetAwaiter().GetResult();
            _channel.QueueBindAsync(RabbitMQConstants.LeaveApprovedQueue, RabbitMQConstants.ExchangeName, RabbitMQConstants.LeaveApprovedQueue).GetAwaiter().GetResult();
            _channel.QueueBindAsync(RabbitMQConstants.LeaveRejectedQueue, RabbitMQConstants.ExchangeName, RabbitMQConstants.LeaveRejectedQueue).GetAwaiter().GetResult();

            _isConnected = true;
            _logger.LogInformation("Connected to RabbitMQ successfully at {Host}:{Port}", rabbitSettings.HostName, rabbitSettings.Port);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not connect to RabbitMQ. Notifications will be logged locally.");
            _isConnected = false;
        }
    }

    public async Task PublishAsync<T>(string queueName, T message)
    {
        var json = JsonSerializer.Serialize(message);

        if (!_isConnected || _channel == null)
        {
            _logger.LogInformation("[NOTIFICATION-FALLBACK] Queue: {Queue}, Message: {Message}", queueName, json);
            return;
        }

        try
        {
            var body = Encoding.UTF8.GetBytes(json);
            var properties = new BasicProperties { Persistent = true };

            await _channel.BasicPublishAsync(
                exchange: RabbitMQConstants.ExchangeName,
                routingKey: queueName,
                mandatory: false,
                basicProperties: properties,
                body: body);

            _logger.LogInformation("Published message to queue {Queue}", queueName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish message to RabbitMQ. Logging locally.");
            _logger.LogInformation("[NOTIFICATION-FALLBACK] Queue: {Queue}, Message: {Message}", queueName, json);
        }
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }
}
