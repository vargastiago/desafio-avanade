using Sales.API.Messaging.Contracts;

namespace Sales.API.Messaging;

public interface IRabbitMQSalesPublisher
{
    Task PublishSaleAsync(SaleNotification notification, CancellationToken stoppingToken);
}
