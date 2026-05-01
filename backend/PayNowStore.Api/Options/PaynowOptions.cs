namespace PayNowStore.Api.Options;

public class PaynowOptions
{
    public const string SectionName = "Paynow";

    public string IntegrationId { get; set; } = string.Empty;
    public string IntegrationKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://www.paynow.co.zw";
    public string ReturnUrl { get; set; } = string.Empty;
    public string ResultUrl { get; set; } = string.Empty;
}
