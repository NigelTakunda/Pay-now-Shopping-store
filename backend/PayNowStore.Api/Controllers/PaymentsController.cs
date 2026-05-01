using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PayNowStore.Api.Data;
using PayNowStore.Api.Dtos;
using PayNowStore.Api.Models;
using PayNowStore.Api.Services;

namespace PayNowStore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController(AppDbContext dbContext, PaynowService paynowService) : ControllerBase
{
    [HttpPost("guest-checkout")]
    public async Task<ActionResult<GuestCheckoutResponse>> GuestCheckout(GuestCheckoutRequest request, CancellationToken cancellationToken)
    {
        if (request.Items.Count == 0)
        {
            return BadRequest(new { message = "Your cart is empty." });
        }

        if (string.IsNullOrWhiteSpace(request.FullName) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Phone) ||
            string.IsNullOrWhiteSpace(request.Address) ||
            string.IsNullOrWhiteSpace(request.City))
        {
            return BadRequest(new { message = "Full name, email, phone, address, and city are required." });
        }

        var normalizedItems = request.Items
            .Where(item => item.Quantity > 0)
            .GroupBy(item => item.ProductId)
            .Select(group => new GuestCheckoutItemRequest(group.Key, group.Sum(item => item.Quantity)))
            .ToList();

        if (normalizedItems.Count == 0)
        {
            return BadRequest(new { message = "Your cart is empty." });
        }

        var productIds = normalizedItems.Select(item => item.ProductId).ToList();
        var products = await dbContext.Products
            .Where(product => productIds.Contains(product.Id) && product.InStock)
            .ToDictionaryAsync(product => product.Id, cancellationToken);

        if (products.Count != productIds.Count)
        {
            return BadRequest(new { message = "One or more selected products are unavailable." });
        }

        var total = normalizedItems.Sum(item => products[item.ProductId].Price * item.Quantity);
        var reference = $"GST-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(100, 999)}";
        var guestUser = await GetOrCreateGuestUserAsync(request, cancellationToken);

        var order = new Order
        {
            UserId = guestUser.Id,
            Reference = reference,
            TotalAmount = total,
            Status = "Pending",
            PaymentStatus = "Pending",
            Items = normalizedItems.Select(item => new OrderItem
            {
                ProductId = item.ProductId,
                ProductName = products[item.ProductId].Name,
                UnitPrice = products[item.ProductId].Price,
                Quantity = item.Quantity
            }).ToList()
        };

        var additionalInfo = BuildAdditionalInfo(request, reference);
        var paymentResult = await paynowService.InitiateTransactionAsync(
            reference,
            total,
            request.Email.Trim(),
            additionalInfo,
            cancellationToken);

        if (!paymentResult.Success)
        {
            return BadRequest(new { message = paymentResult.Error ?? "Unable to initiate Paynow transaction." });
        }

        order.RedirectUrl = paymentResult.RedirectUrl;
        order.PollUrl = paymentResult.PollUrl;
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new GuestCheckoutResponse(order.Id, order.Reference, paymentResult.RedirectUrl, paymentResult.PollUrl, total));
    }

    [HttpGet("order-status/{reference}")]
    public async Task<ActionResult<OrderDto>> GetOrderStatus(string reference, [FromQuery] bool refresh = false, CancellationToken cancellationToken = default)
    {
        var order = await dbContext.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Reference == reference, cancellationToken);

        if (order is null)
        {
            return NotFound();
        }

        if (refresh && !string.IsNullOrWhiteSpace(order.PollUrl))
        {
            var status = await paynowService.PollStatusAsync(order.PollUrl, cancellationToken);
            if (status.Success)
            {
                order.PaynowReference = status.PaynowReference ?? order.PaynowReference;
                order.PollUrl = status.PollUrl ?? order.PollUrl;
                order.PaymentStatus = status.Status;

                if (status.Status.Equals("Paid", StringComparison.OrdinalIgnoreCase) ||
                    status.Status.Equals("Awaiting Delivery", StringComparison.OrdinalIgnoreCase) ||
                    status.Status.Equals("Delivered", StringComparison.OrdinalIgnoreCase))
                {
                    order.Status = "Paid";
                }

                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        return Ok(new OrderDto(
            order.Id,
            order.Reference,
            order.TotalAmount,
            order.Status,
            order.PaymentStatus,
            order.RedirectUrl,
            order.CreatedAtUtc,
            order.Items.Select(item => new OrderItemDto(item.ProductName, item.UnitPrice, item.Quantity)).ToList()));
    }

    [HttpPost("callback")]
    public async Task<IActionResult> Callback(CancellationToken cancellationToken)
    {
        var form = await Request.ReadFormAsync(cancellationToken);
        var payload = form.ToDictionary(entry => entry.Key, entry => entry.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        if (!paynowService.ValidateHash(payload))
        {
            return BadRequest("Invalid hash.");
        }

        var reference = payload.GetValueOrDefault("reference");
        if (string.IsNullOrWhiteSpace(reference))
        {
            return BadRequest("Missing reference.");
        }

        var order = await dbContext.Orders.FirstOrDefaultAsync(o => o.Reference == reference, cancellationToken);
        if (order is null)
        {
            return NotFound();
        }

        order.PaynowReference = payload.GetValueOrDefault("paynowreference") ?? order.PaynowReference;
        order.PollUrl = payload.GetValueOrDefault("pollurl") ?? order.PollUrl;
        order.PaymentStatus = payload.GetValueOrDefault("status") ?? order.PaymentStatus;

        if (order.PaymentStatus.Equals("Paid", StringComparison.OrdinalIgnoreCase) ||
            order.PaymentStatus.Equals("Awaiting Delivery", StringComparison.OrdinalIgnoreCase) ||
            order.PaymentStatus.Equals("Delivered", StringComparison.OrdinalIgnoreCase))
        {
            order.Status = "Paid";
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok("OK");
    }

    private async Task<User> GetOrCreateGuestUserAsync(GuestCheckoutRequest request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await dbContext.Users.FirstOrDefaultAsync(existing => existing.Email == normalizedEmail, cancellationToken);
        if (user is not null)
        {
            user.FullName = request.FullName.Trim();
            return user;
        }

        user = new User
        {
            Name = request.FullName.Trim(),
            Surname = "Guest",
            FullName = request.FullName.Trim(),
            Email = normalizedEmail,
            PasswordHash = "GUEST-CHECKOUT",
            Role = "Customer"
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);
        return user;
    }

    private static string BuildAdditionalInfo(GuestCheckoutRequest request, string reference)
    {
        var note = string.IsNullOrWhiteSpace(request.Notes) ? "No extra notes" : request.Notes.Trim();
        return $"Order {reference} for {request.FullName.Trim()} | Phone: {request.Phone.Trim()} | City: {request.City.Trim()} | Address: {request.Address.Trim()} | Notes: {note}";
    }
}
