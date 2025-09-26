namespace Sales.API.Dtos;

public class OrderCreateDto
{
    public string? CustomerId { get; set; }
    public List<OrderItemDto> Items { get; set; } = [];
}
