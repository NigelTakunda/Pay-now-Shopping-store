using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PayNowStore.Api.Data;
using PayNowStore.Api.Dtos;
using PayNowStore.Api.Services;

namespace PayNowStore.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/[controller]")]
public class AdminController(AppDbContext dbContext, CurrentUserService currentUserService) : ControllerBase
{
    [HttpGet("products")]
    public async Task<ActionResult<IReadOnlyList<ProductDto>>> GetProducts(CancellationToken cancellationToken)
    {
        var products = await dbContext.Products
            .OrderBy(product => product.Id)
            .Select(product => new ProductDto(
                product.Id,
                product.Name,
                product.Description,
                product.Price,
                product.ImageUrl,
                product.Category,
                product.InStock))
            .ToListAsync(cancellationToken);

        return Ok(products);
    }

    [HttpPost("products")]
    public async Task<ActionResult<ProductDto>> UpsertProduct(AdminProductUpsertRequest request, CancellationToken cancellationToken)
    {
        var product = await dbContext.Products.FirstOrDefaultAsync(existing => existing.Id == request.Id, cancellationToken);

        if (product is null)
        {
            product = new Models.Product { Id = request.Id };
            dbContext.Products.Add(product);
        }

        product.Name = request.Name.Trim();
        product.Description = request.Description.Trim();
        product.Category = request.Category.Trim();
        product.ImageUrl = request.ImageUrl.Trim();
        product.Price = request.Price;
        product.InStock = true;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new ProductDto(product.Id, product.Name, product.Description, product.Price, product.ImageUrl, product.Category, product.InStock));
    }

    [HttpDelete("products/{id:int}")]
    public async Task<IActionResult> RemoveProduct(int id, CancellationToken cancellationToken)
    {
        var product = await dbContext.Products.FirstOrDefaultAsync(existing => existing.Id == id, cancellationToken);
        if (product is null)
        {
            return NotFound(new { message = "Product not found." });
        }

        product.InStock = false;
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("customers/{id:int}")]
    public async Task<IActionResult> DeleteCustomer(int id, CancellationToken cancellationToken)
    {
        var currentAdminId = currentUserService.GetUserId();
        if (id == currentAdminId)
        {
            return BadRequest(new { message = "Administrators cannot delete their own account." });
        }

        var user = await dbContext.Users
            .Include(existing => existing.CartItems)
            .Include(existing => existing.Orders)
                .ThenInclude(order => order.Items)
            .FirstOrDefaultAsync(existing => existing.Id == id, cancellationToken);

        if (user is null)
        {
            return NotFound(new { message = "Customer not found." });
        }

        if (!user.Role.Equals("Customer", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Only customer accounts can be deleted from this action." });
        }

        dbContext.CartItems.RemoveRange(user.CartItems);
        foreach (var order in user.Orders)
        {
            dbContext.OrderItems.RemoveRange(order.Items);
        }

        dbContext.Orders.RemoveRange(user.Orders);
        dbContext.Users.Remove(user);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<AdminDashboardDto>> GetDashboard(CancellationToken cancellationToken)
    {
        var userEntities = await dbContext.Users
            .Include(user => user.CartItems)
                .ThenInclude(item => item.Product)
            .Include(user => user.Orders)
                .ThenInclude(order => order.Items)
            .OrderByDescending(user => user.LastLoginAtUtc ?? user.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var users = userEntities
            .Select(user => new AdminUserDto(
                user.Id,
                user.FullName,
                user.Email,
                user.Role,
                user.CreatedAtUtc,
                user.LastLoginAtUtc,
                user.CartItems
                    .Select(item => new AdminCartItemDto(
                        item.Id,
                        item.ProductId,
                        item.Product?.Name ?? "Unknown product",
                        item.Quantity,
                        item.Product?.Price ?? 0,
                        (item.Product?.Price ?? 0) * item.Quantity))
                    .ToList(),
                user.Orders
                    .OrderByDescending(order => order.CreatedAtUtc)
                    .Select(order => new AdminOrderDto(
                        order.Id,
                        order.Reference,
                        order.TotalAmount,
                        order.Status,
                        order.PaymentStatus,
                        order.CreatedAtUtc,
                        order.Items
                            .Select(item => new AdminOrderItemDto(item.ProductName, item.Quantity, item.UnitPrice))
                            .ToList()))
                    .ToList()))
            .ToList();

        var customerUsers = users.Where(user => user.Role.Equals("Customer", StringComparison.OrdinalIgnoreCase)).ToList();
        var totalPayments = customerUsers
            .SelectMany(user => user.Orders)
            .Where(order => order.PaymentStatus.Equals("Paid", StringComparison.OrdinalIgnoreCase) ||
                            order.Status.Equals("Paid", StringComparison.OrdinalIgnoreCase))
            .Sum(order => order.TotalAmount);

        return Ok(new AdminDashboardDto(
            customerUsers.Count,
            customerUsers.Count(user => user.CartItems.Count > 0),
            customerUsers.Sum(user => user.Orders.Count),
            totalPayments,
            users));
    }
}
