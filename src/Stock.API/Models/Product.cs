using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Stock.API.Models;

public class Product
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(100)]
    public string Name { get; set; } = null!;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Precision(18, 2)]
    public decimal Price { get; set; }

    public int StockQuantity { get; set; }
}
