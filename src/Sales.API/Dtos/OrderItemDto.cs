namespace Sales.API.Dtos;

public class OrderItemDto
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
}
