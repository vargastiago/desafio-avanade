using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Sales.API.Models;

public class OrderItem
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }

    [Precision(18, 2)]
    public decimal UnitPrice { get; set; }

    public Guid OrderId { get; set; }
    public Order? Order { get; set; }
}
