namespace PayNowStore.Api.Dtos;

public record AdminCartItemDto(int Id, int ProductId, string ProductName, int Quantity, decimal UnitPrice, decimal LineTotal);
public record AdminOrderItemDto(string ProductName, int Quantity, decimal UnitPrice);
public record AdminOrderDto(int Id, string Reference, decimal TotalAmount, string Status, string PaymentStatus, DateTime CreatedAtUtc, IReadOnlyList<AdminOrderItemDto> Items);
public record AdminUserDto(
    int Id,
    string FullName,
    string Email,
    string Role,
    DateTime CreatedAtUtc,
    DateTime? LastLoginAtUtc,
    IReadOnlyList<AdminCartItemDto> CartItems,
    IReadOnlyList<AdminOrderDto> Orders);
public record AdminDashboardDto(int TotalUsers, int ActiveCarts, int TotalOrders, decimal TotalPayments, IReadOnlyList<AdminUserDto> Users);
