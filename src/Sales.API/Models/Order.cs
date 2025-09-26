using System.ComponentModel.DataAnnotations;

namespace Sales.API.Models;

public class Order
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public string? CustomerId { get; set; }
    public List<OrderItem> Items { get; set; } = [];

    public decimal Total => Items.Sum(i => i.UnitPrice * i.Quantity);
}
