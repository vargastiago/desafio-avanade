using Microsoft.EntityFrameworkCore;
using Sales.API.Models;

namespace Sales.API.Data;

public class SalesDbContext : DbContext
{
    public SalesDbContext(DbContextOptions<SalesDbContext> options) : base(options) { }

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
}
