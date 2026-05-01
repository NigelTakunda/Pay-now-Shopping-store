using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PayNowStore.Api.Data;
using PayNowStore.Api.Dtos;
using PayNowStore.Api.Models;
using PayNowStore.Api.Services;

namespace PayNowStore.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class CartController(AppDbContext dbContext, CurrentUserService currentUserService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CartSummaryDto>> GetCart()
    {
        var userId = currentUserService.GetUserId();
        return Ok(await BuildCartAsync(userId));
    }

    [HttpPost]
    public async Task<ActionResult<CartSummaryDto>> AddToCart(AddCartItemRequest request)
    {
        var userId = currentUserService.GetUserId();
        var product = await dbContext.Products.FindAsync(request.ProductId);
        if (product is null || !product.InStock)
        {
            return BadRequest(new { message = "Product is not available." });
        }

        var quantity = Math.Max(1, request.Quantity);
        var existing = await dbContext.CartItems.FirstOrDefaultAsync(ci => ci.UserId == userId && ci.ProductId == request.ProductId);
        if (existing is null)
        {
            dbContext.CartItems.Add(new CartItem { ProductId = request.ProductId, Quantity = quantity, UserId = userId });
        }
        else
        {
            existing.Quantity += quantity;
        }

        await dbContext.SaveChangesAsync();
        return Ok(await BuildCartAsync(userId));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<CartSummaryDto>> UpdateCartItem(int id, UpdateCartItemRequest request)
    {
        var userId = currentUserService.GetUserId();
        var item = await dbContext.CartItems.FirstOrDefaultAsync(ci => ci.Id == id && ci.UserId == userId);
        if (item is null)
        {
            return NotFound();
        }

        item.Quantity = Math.Max(1, request.Quantity);
        await dbContext.SaveChangesAsync();
        return Ok(await BuildCartAsync(userId));
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<CartSummaryDto>> RemoveCartItem(int id)
    {
        var userId = currentUserService.GetUserId();
        var item = await dbContext.CartItems.FirstOrDefaultAsync(ci => ci.Id == id && ci.UserId == userId);
        if (item is null)
        {
            return NotFound();
        }

        dbContext.CartItems.Remove(item);
        await dbContext.SaveChangesAsync();
        return Ok(await BuildCartAsync(userId));
    }

    private async Task<CartSummaryDto> BuildCartAsync(int userId)
    {
        var items = await dbContext.CartItems
            .Where(ci => ci.UserId == userId)
            .Include(ci => ci.Product)
            .Select(ci => new CartItemDto(
                ci.Id,
                ci.ProductId,
                ci.Product!.Name,
                ci.Product.ImageUrl,
                ci.Product.Price,
                ci.Quantity,
                ci.Product.Price * ci.Quantity))
            .ToListAsync();

        return new CartSummaryDto(items, items.Sum(item => item.LineTotal));
    }
}
