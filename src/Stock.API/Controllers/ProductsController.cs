using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stock.API.Data;
using Stock.API.Dtos;
using Stock.API.Models;

namespace Stock.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProductsController : ControllerBase
{
    private readonly StockDbContext _context;

    public ProductsController(StockDbContext context)
    {
        _context = context;
    }

    public async Task<ActionResult<IEnumerable<ProductReadDto>>> GetAll()
    {
        var products = await _context.Products
            .AsNoTracking()
            .Select(p => new ProductReadDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                Price = p.Price,
                StockQuantity = p.StockQuantity
            })
            .ToListAsync();

        return Ok(products);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductReadDto>> GetById(Guid id)
    {
        var product = await _context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (product is null)
        {
            return NotFound();
        }

        return Ok(new ProductReadDto
        {
            Id = product.Id,
            Name = product.Name,
            Description = product.Description,
            Price = product.Price,
            StockQuantity = product.StockQuantity
        });

    }

    [HttpPost]
    public async Task<ActionResult<ProductReadDto>> Create(ProductCreateDto productCreateDto)
    {
        var product = new Product
        {
            Name = productCreateDto.Name,
            Description = productCreateDto.Description,
            Price = productCreateDto.Price,
            StockQuantity = productCreateDto.StockQuantity
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        var productReadDto = new ProductReadDto
        {
            Id = product.Id,
            Name = product.Name,
            Description = product.Description,
            Price = product.Price,
            StockQuantity = product.StockQuantity
        };

        return CreatedAtAction(nameof(GetById), new { id = product.Id }, productReadDto);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, ProductUpdateDto dto)
    {
        var p = await _context.Products.FindAsync(id);

        if (p is null)
        {
            return NotFound();
        }

        if (dto.Description is not null)
        {
            p.Description = dto.Description;
        }

        if (dto.Price.HasValue)
        {
            p.Price = dto.Price.Value;
        }

        if (dto.StockQuantity.HasValue)
        {
            p.StockQuantity = dto.StockQuantity.Value;
        }

        await _context.SaveChangesAsync();
        return NoContent();
    }
}
