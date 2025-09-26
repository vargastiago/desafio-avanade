namespace Sales.API.Dtos;

public class OrderReadDto
{
    public Guid Id { get; set; }
    public string? CustomerId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; } = null!;
    public decimal Total { get; set; }
    public List<OrderItemDto> Items { get; set; } = [];
}
