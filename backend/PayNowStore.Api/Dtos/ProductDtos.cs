namespace PayNowStore.Api.Dtos;

public record ProductDto(int Id, string Name, string Description, decimal Price, string ImageUrl, string Category, bool InStock);
public record AdminProductUpsertRequest(int Id, string Name, string Description, string Category, string ImageUrl, decimal Price);
public record AdminProductRemoveRequest(int Id);
