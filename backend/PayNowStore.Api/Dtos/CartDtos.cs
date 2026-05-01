namespace PayNowStore.Api.Dtos;

public record AddCartItemRequest(int ProductId, int Quantity);
public record UpdateCartItemRequest(int Quantity);
public record CartItemDto(int Id, int ProductId, string Name, string ImageUrl, decimal Price, int Quantity, decimal LineTotal);
public record CartSummaryDto(IReadOnlyList<CartItemDto> Items, decimal Total);
