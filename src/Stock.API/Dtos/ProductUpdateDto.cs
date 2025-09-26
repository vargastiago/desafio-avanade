namespace Stock.API.Dtos;

public class ProductUpdateDto
{
    public string? Description { get; set; }
    public decimal? Price { get; set; }
    public int? StockQuantity { get; set; }
}
