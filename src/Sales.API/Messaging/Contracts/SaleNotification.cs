namespace Sales.API.Messaging.Contracts;

public class SaleNotification
{
    public Guid OrderId { get; set; }
    public List<SaleItem> Items { get; set; } = [];
}
