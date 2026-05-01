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
public class OrdersController(AppDbContext dbContext, CurrentUserService currentUserService, PaynowService paynowService) : ControllerBase
{
    [HttpPost("checkout")]
    public async Task<ActionResult<CheckoutResponse>> Checkout(CheckoutRequest request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetUserId();
        var user = await dbContext.Users.FindAsync([userId], cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        var cartItems = await dbContext.CartItems
            .Where(ci => ci.UserId == userId)
            .Include(ci => ci.Product)
            .ToListAsync(cancellationToken);

        if (cartItems.Count == 0)
        {
            return BadRequest(new { message = "Your cart is empty." });
        }

        var total = cartItems.Sum(ci => ci.Product!.Price * ci.Quantity);
        var reference = $"ORD-{DateTime.UtcNow:yyyyMMddHHmmss}-{userId}";
        var order = new Order
        {
            UserId = userId,
            Reference = reference,
            TotalAmount = total,
            Status = "Pending",
            PaymentStatus = "Pending",
            Items = cartItems.Select(ci => new OrderItem
            {
                ProductId = ci.ProductId,
                ProductName = ci.Product!.Name,
                UnitPrice = ci.Product.Price,
                Quantity = ci.Quantity
            }).ToList()
        };

        var additionalInfo = request.Notes?.Trim();
        if (string.IsNullOrWhiteSpace(additionalInfo))
        {
            additionalInfo = $"Payment for order {reference}";
        }

        var paymentResult = await paynowService.InitiateTransactionAsync(reference, total, user.Email, additionalInfo, cancellationToken);
        if (!paymentResult.Success)
        {
            return BadRequest(new { message = paymentResult.Error ?? "Unable to initiate Paynow transaction." });
        }

        order.RedirectUrl = paymentResult.RedirectUrl;
        order.PollUrl = paymentResult.PollUrl;
        dbContext.Orders.Add(order);
        dbContext.CartItems.RemoveRange(cartItems);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new CheckoutResponse(order.Id, order.Reference, paymentResult.RedirectUrl, paymentResult.PollUrl));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OrderDto>>> GetOrders()
    {
        var userId = currentUserService.GetUserId();
        var orders = await dbContext.Orders
            .Where(o => o.UserId == userId)
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAtUtc)
            .Select(o => new OrderDto(
                o.Id,
                o.Reference,
                o.TotalAmount,
                o.Status,
                o.PaymentStatus,
                o.RedirectUrl,
                o.CreatedAtUtc,
                o.Items.Select(i => new OrderItemDto(i.ProductName, i.UnitPrice, i.Quantity)).ToList()))
            .ToListAsync();

        return Ok(orders);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderDto>> GetOrder(int id, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetUserId();
        var order = await dbContext.Orders
            .Where(o => o.Id == id && o.UserId == userId)
            .Include(o => o.Items)
            .Select(o => new OrderDto(
                o.Id,
                o.Reference,
                o.TotalAmount,
                o.Status,
                o.PaymentStatus,
                o.RedirectUrl,
                o.CreatedAtUtc,
                o.Items.Select(i => new OrderItemDto(i.ProductName, i.UnitPrice, i.Quantity)).ToList()))
            .FirstOrDefaultAsync(cancellationToken);

        return order is null ? NotFound() : Ok(order);
    }

    [HttpPost("{id:int}/refresh-status")]
    public async Task<ActionResult<OrderDto>> RefreshStatus(int id, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetUserId();
        var order = await dbContext.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId, cancellationToken);

        if (order is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(order.PollUrl))
        {
            return BadRequest(new { message = "No Paynow poll URL is stored for this order." });
        }

        var status = await paynowService.PollStatusAsync(order.PollUrl, cancellationToken);
        ApplyStatus(order, status);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new OrderDto(
            order.Id,
            order.Reference,
            order.TotalAmount,
            order.Status,
            order.PaymentStatus,
            order.RedirectUrl,
            order.CreatedAtUtc,
            order.Items.Select(i => new OrderItemDto(i.ProductName, i.UnitPrice, i.Quantity)).ToList()));
    }

    private static void ApplyStatus(Order order, PaynowStatusResult status)
    {
        if (!status.Success)
        {
            return;
        }

        order.PaynowReference = status.PaynowReference ?? order.PaynowReference;
        order.PollUrl = status.PollUrl ?? order.PollUrl;
        order.PaymentStatus = status.Status;

        if (status.Status.Equals("Paid", StringComparison.OrdinalIgnoreCase) ||
            status.Status.Equals("Awaiting Delivery", StringComparison.OrdinalIgnoreCase) ||
            status.Status.Equals("Delivered", StringComparison.OrdinalIgnoreCase))
        {
            order.Status = "Paid";
        }
    }
}
