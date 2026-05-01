namespace PayNowStore.Api.Models;

public class Order
{
    public int Id { get; set; }
    public string Reference { get; set; } = string.Empty;
    public int UserId { get; set; }
    public User? User { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "Pending";
    public string PaymentStatus { get; set; } = "Pending";
    public string? PaynowReference { get; set; }
    public string? PollUrl { get; set; }
    public string? RedirectUrl { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<OrderItem> Items { get; set; } = [];
}
