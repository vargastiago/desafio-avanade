namespace Stock.API.Messaging.Contracts;

public class SaleItem
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
}
