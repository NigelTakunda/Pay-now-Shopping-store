namespace PayNowStore.Api.Dtos;

public record CheckoutRequest(string? Notes);
public record CheckoutResponse(int OrderId, string Reference, string RedirectUrl, string PollUrl);
public record GuestCheckoutItemRequest(int ProductId, int Quantity);
public record GuestCheckoutRequest(
    string FullName,
    string Email,
    string Phone,
    string Address,
    string City,
    string? Notes,
    IReadOnlyList<GuestCheckoutItemRequest> Items);
public record GuestCheckoutResponse(int OrderId, string Reference, string RedirectUrl, string PollUrl, decimal TotalAmount);
public record OrderItemDto(string ProductName, decimal UnitPrice, int Quantity);
public record OrderDto(int Id, string Reference, decimal TotalAmount, string Status, string PaymentStatus, string? RedirectUrl, DateTime CreatedAtUtc, IReadOnlyList<OrderItemDto> Items);
