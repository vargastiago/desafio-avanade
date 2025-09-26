using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Stock.API.Data;
using Stock.API.Messaging.Contracts;

namespace Stock.API.Messaging;

public class SaleNotificationConsumer : BackgroundService, IAsyncDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SaleNotificationConsumer> _logger;
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    private IConnection? _connection;
    private IChannel? _channel;
    private readonly string _queueName;
    private readonly string _exchangeName;
    private readonly string _routingKey;
    private readonly ushort _prefetchCount;

    public SaleNotificationConsumer(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        JsonSerializerOptions jsonSerializerOptions,
        ILogger<SaleNotificationConsumer> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _jsonSerializerOptions = jsonSerializerOptions;
        _logger = logger;

        _queueName = configuration["RabbitMQ:QueueName"]!;
        _exchangeName = configuration["RabbitMQ:Exchange"]!;
        _routingKey = configuration["RabbitMQ:RoutingKey"]!;
        _prefetchCount = configuration.GetValue<ushort>("RabbitMQ:PrefetchCount");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EnsureInitializeAsync(stoppingToken);
        var consumer = new AsyncEventingBasicConsumer(_channel!);

        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                var sale = JsonSerializer.Deserialize<SaleNotification>(json, _jsonSerializerOptions);

                if (sale is not null)
                {
                    _logger.LogInformation("Received sale notification for Order {OrderId}.", sale.OrderId);
                    await ProcessSaleAsync(sale, stoppingToken);
                }
                else
                {
                    _logger.LogWarning("Failed to deserialize sale notification: {Json}.", json);
                }

                await _channel!.BasicAckAsync(
                    deliveryTag: ea.DeliveryTag,
                    multiple: false,
                    cancellationToken: stoppingToken
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing sale message.");
                await _channel!.BasicNackAsync(
                    deliveryTag: ea.DeliveryTag,
                    multiple: false,
                    requeue: false,
                    cancellationToken: stoppingToken
                );
            }
        };

        await _channel!.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: _prefetchCount,
            global: false,
            cancellationToken: stoppingToken
        );

        await _channel.BasicConsumeAsync(
            queue: _queueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken
        );

        _logger.LogInformation(
            "Started consuming queue {Queue} with prefetch {Prefetch}",
            _queueName, _prefetchCount);
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
            ClientProvidedName = "StockConsumer"
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
            exchange: _exchangeName,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: stoppingToken);

        await _channel.QueueBindAsync(
            queue: _queueName,
            exchange: _exchangeName,
            routingKey: _routingKey,
            arguments: null,
            cancellationToken: stoppingToken);

        _logger.LogInformation(
            "RabbitMQ consumer initialized. " +
            "Queue={Queue}, Exchange={Exchange}, RoutingKey={RoutingKey}.",
            _queueName, _exchangeName, _routingKey);
    }

    private async Task ProcessSaleAsync(SaleNotification sale, CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StockDbContext>();

        foreach (var item in sale.Items)
        {
            var affectedRows = await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE Products
                SET StockQuantity = StockQuantity - {item.Quantity}
                WHERE Id = {item.ProductId} AND StockQuantity >= {item.Quantity}
                """,
                stoppingToken);

            if (affectedRows > 0)
            {
                _logger.LogInformation(
                    "Product {ProductId} stock reduced by {Qty} for Order {OrderId}.",
                    item.ProductId, item.Quantity, sale.OrderId);
            }
            else
            {
                _logger.LogWarning(
                    "Failed to reduce stock for Product {ProductId} (Order {OrderId}). " +
                    "Reason: Product not found or insufficient stock.",
                    item.ProductId, sale.OrderId);
            }
        }
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
