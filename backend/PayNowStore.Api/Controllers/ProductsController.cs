using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PayNowStore.Api.Data;
using PayNowStore.Api.Dtos;

namespace PayNowStore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProductDto>>> GetProducts()
    {
        var products = await dbContext.Products
            .Where(p => p.InStock)
            .OrderBy(p => p.Id)
            .Select(p => new ProductDto(p.Id, p.Name, p.Description, p.Price, p.ImageUrl, p.Category, p.InStock))
            .ToListAsync();

        return Ok(products);
    }
}
