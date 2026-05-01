using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using PayNowStore.Api.Options;

namespace PayNowStore.Api.Services;

public class PaynowService(HttpClient httpClient, IOptions<PaynowOptions> options)
{
    private readonly PaynowOptions _options = options.Value;

    public async Task<PaynowInitiationResult> InitiateTransactionAsync(string reference, decimal amount, string email, string additionalInfo, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.IntegrationId) ||
            string.IsNullOrWhiteSpace(_options.IntegrationKey) ||
            _options.IntegrationId.Contains("YOUR_PAYNOW", StringComparison.OrdinalIgnoreCase) ||
            _options.IntegrationKey.Contains("YOUR_PAYNOW", StringComparison.OrdinalIgnoreCase))
        {
            return new PaynowInitiationResult(
                false,
                string.Empty,
                string.Empty,
                "PayNow integration credentials are missing. Update the Paynow settings in appsettings.json or user secrets.");
        }

        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = _options.IntegrationId,
            ["reference"] = reference,
            ["amount"] = amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            ["additionalinfo"] = additionalInfo,
            ["returnurl"] = _options.ReturnUrl,
            ["resulturl"] = _options.ResultUrl,
            ["authemail"] = email,
            ["status"] = "Message"
        };

        fields["hash"] = GenerateHash(fields, _options.IntegrationKey);

        using var response = await httpClient.PostAsync(
            $"{_options.BaseUrl.TrimEnd('/')}/interface/initiatetransaction",
            new FormUrlEncodedContent(fields),
            cancellationToken);

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var payload = ParsePayload(content);
        if (!payload.TryGetValue("status", out var status))
        {
            return new PaynowInitiationResult(false, string.Empty, string.Empty, "Invalid response from Paynow.");
        }

        if (status.Equals("Error", StringComparison.OrdinalIgnoreCase))
        {
            return new PaynowInitiationResult(false, string.Empty, string.Empty, payload.GetValueOrDefault("error"));
        }

        if (!ValidateHash(payload))
        {
            return new PaynowInitiationResult(false, string.Empty, string.Empty, "Paynow response hash validation failed.");
        }

        return new PaynowInitiationResult(
            true,
            payload.GetValueOrDefault("browserurl") ?? string.Empty,
            payload.GetValueOrDefault("pollurl") ?? string.Empty,
            null);
    }

    public async Task<PaynowStatusResult> PollStatusAsync(string pollUrl, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsync(pollUrl, new StringContent(string.Empty), cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var payload = ParsePayload(content);

        if (!payload.TryGetValue("status", out var status))
        {
            return new PaynowStatusResult(false, "Unknown", null, null, "Invalid status response.");
        }

        if (status.Equals("Error", StringComparison.OrdinalIgnoreCase))
        {
            return new PaynowStatusResult(false, status, null, null, payload.GetValueOrDefault("error"));
        }

        if (!status.Equals("NotFound", StringComparison.OrdinalIgnoreCase) && !ValidateHash(payload))
        {
            return new PaynowStatusResult(false, "InvalidHash", null, null, "Hash validation failed.");
        }

        return new PaynowStatusResult(
            true,
            status,
            payload.GetValueOrDefault("paynowreference"),
            payload.GetValueOrDefault("pollurl"),
            null);
    }

    public Dictionary<string, string> ParsePayload(string rawPayload)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in rawPayload.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(parts[0]).Trim();
            var value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]).Trim() : string.Empty;
            result[key] = value;
        }

        return result;
    }

    public bool ValidateHash(Dictionary<string, string> values)
    {
        if (!values.TryGetValue("hash", out var providedHash) || string.IsNullOrWhiteSpace(providedHash))
        {
            return false;
        }

        var generatedHash = GenerateHash(values, _options.IntegrationKey);
        return string.Equals(providedHash, generatedHash, StringComparison.OrdinalIgnoreCase);
    }

    public static string GenerateHash(IReadOnlyDictionary<string, string> values, string integrationKey)
    {
        var concatenated = string.Concat(values
            .Where(pair => !pair.Key.Equals("hash", StringComparison.OrdinalIgnoreCase))
            .Select(pair => pair.Value?.Trim() ?? string.Empty));

        using var sha = SHA512.Create();
        var hashBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(concatenated + integrationKey));
        return Convert.ToHexString(hashBytes);
    }
}
