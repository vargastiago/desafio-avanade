using System.Text;
using System.Text.Json;
using Polly;
using Polly.Retry;
using RabbitMQ.Client;
using Sales.API.Messaging.Contracts;

namespace Sales.API.Messaging;

public class RabbitMQSalesPublisher : IRabbitMQSalesPublisher, IAsyncDisposable
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<RabbitMQSalesPublisher> _logger;

    private IConnection? _connection;
    private IChannel? _channel;

    private readonly string _queueName;
    private readonly string _exchange;
    private readonly string _routingKey;

    private readonly AsyncRetryPolicy _retryPolicy;

    public RabbitMQSalesPublisher(
        IConfiguration configuration,
        ILogger<RabbitMQSalesPublisher> logger)
    {
        _configuration = configuration;
        _logger = logger;

        _queueName = configuration["RabbitMQ:QueueName"]!;
        _exchange = configuration["RabbitMQ:Exchange"]!;
        _routingKey = configuration["RabbitMQ:RoutingKey"]!;

        _retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (ex, ts, attempt, ctx) =>
                {
                    _logger.LogWarning(ex, "Error publishing message. Attempt {Attempt}.", attempt);
                }
            );
    }

    public async Task PublishSaleAsync(SaleNotification notification, CancellationToken stoppingToken)
    {
        await EnsureInitializeAsync(stoppingToken);

        var json = JsonSerializer.Serialize(notification);
        var body = Encoding.UTF8.GetBytes(json);

        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent
        };

        await _retryPolicy.ExecuteAsync(async ct =>
        {
            await _channel!.BasicPublishAsync(
                exchange: _exchange,
                routingKey: _routingKey,
                mandatory: false,
                basicProperties: properties,
                body: body,
                cancellationToken: ct
            );

            _logger.LogInformation("Published sale notification for Order {OrderId} (items: {Count}).",
            notification.OrderId, notification.Items.Count);

        }, stoppingToken);
    }

    private async Task EnsureInitializeAsync(CancellationToken stoppingToken)
    {
        if (_connection is not null && _connection.IsOpen &&
            _channel is not null && _channel.IsOpen)
        {
            return;
        }

        var factory = new ConnectionFactory
        {
            HostName = _configuration["RabbitMQ:Hostname"]!,
            UserName = _configuration["RabbitMQ:Username"]!,
            Password = _configuration["RabbitMQ:Password"]!,
            AutomaticRecoveryEnabled = true,
            ClientProvidedName = "SalesPublisher"
        };

        _connection = await factory.CreateConnectionAsync(stoppingToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await _channel.QueueDeclareAsync(
            queue: _queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: stoppingToken
        );

        await _channel.ExchangeDeclareAsync(
            exchange: _exchange,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: stoppingToken
        );

        await _channel.QueueBindAsync(
            queue: _queueName,
            exchange: _exchange,
            routingKey: _routingKey,
            arguments: null,
            cancellationToken: stoppingToken
        );

        _logger.LogInformation(
                "RabbitMQ publisher initialized. " +
                "Queue={Queue}, Exchange={Exchange}, RoutingKey={RoutingKey}.",
                _queueName, _exchange, _routingKey);
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync();

        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }
}
