using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sales.API.Data;
using Sales.API.Dtos;
using Sales.API.Messaging;
using Sales.API.Messaging.Contracts;
using Sales.API.Models;

namespace Sales.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly SalesDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IRabbitMQSalesPublisher _publisher;
    private ILogger<OrdersController> _logger;

    public OrdersController(
        SalesDbContext context,
        IHttpClientFactory httpClientFactory,
        IRabbitMQSalesPublisher publisher,
        ILogger<OrdersController> logger)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _publisher = publisher;
        _logger = logger;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderReadDto>> GetById(Guid id)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order is null)
        {
            return NotFound();
        }

        var orderReadDto = new OrderReadDto
        {
            Id = order.Id,
            CustomerId = order.CustomerId,
            CreatedAt = order.CreatedAt,
            Status = order.Status.ToString(),
            Total = order.Total,
            Items = [.. order.Items
                .Select(i => new OrderItemDto { ProductId = i.ProductId, Quantity = i.Quantity })]
        };

        return Ok(orderReadDto);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        OrderCreateDto orderCreateDto,
        CancellationToken stoppingToken)
    {
        if (orderCreateDto.Items is null || orderCreateDto.Items.Count == 0)
        {
            return BadRequest("Order must have at least one item.");
        }

        // Validar estoque
        var client = _httpClientFactory.CreateClient("StockService");
        var token = await HttpContext.GetTokenAsync("access_token");

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var stockCheckFailures = new List<string>();
        var unitPrices = new Dictionary<Guid, decimal>();

        foreach (var item in orderCreateDto.Items)
        {
            var url = $"/api/products/{item.ProductId}";
            var response = await client.GetAsync(url, stoppingToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Error querying stock service for product {ProductId}: {Status}.",
                    item.ProductId, response.StatusCode);
                return StatusCode((int)HttpStatusCode.BadGateway, "Error querying stock service.");
            }

            var json = await response.Content.ReadAsStringAsync(stoppingToken);
            using var document = JsonDocument.Parse(json);

            var root = document.RootElement;
            var stockQuantity = root.GetProperty("stockQuantity").GetInt32();
            var price = root.GetProperty("price").GetDecimal();

            unitPrices[item.ProductId] = price;

            if (stockQuantity < item.Quantity)
            {
                stockCheckFailures.Add(
                    $"Product {item.ProductId} is out of stock. " +
                    $"Available: {stockQuantity}, ordered: {item.Quantity}.");
            }
        }

        if (stockCheckFailures.Count != 0)
        {
            return BadRequest(new { message = "insufficient stock", details = stockCheckFailures });
        }

        var order = new Order
        {
            CustomerId = orderCreateDto.CustomerId,
            Status = OrderStatus.Confirmed
        };

        foreach (var item in orderCreateDto.Items)
        {
            order.Items.Add(new OrderItem
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitPrice = unitPrices[item.ProductId]
            });
        }

        _context.Orders.Add(order);
        await _context.SaveChangesAsync(stoppingToken);

        // Publicar notificação de venda
        var saleNotification = new SaleNotification
        {
            OrderId = order.Id,
            Items = [.. order.Items
                .Select(i => new SaleItem { ProductId = i.ProductId, Quantity = i.Quantity })]
        };

        try
        {
            await _publisher.PublishSaleAsync(saleNotification, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error posting sales notification for order {OrderId}.", order.Id);
        }

        var orderReadDto = new OrderReadDto
        {
            Id = order.Id,
            CustomerId = order.CustomerId,
            CreatedAt = order.CreatedAt,
            Status = order.Status.ToString(),
            Total = order.Total,
            Items = [.. order.Items
                .Select(i => new OrderItemDto { ProductId = i.ProductId, Quantity = i.Quantity })]
        };

        return CreatedAtAction(nameof(GetById), new { id = order.Id }, orderReadDto);
    }
}
