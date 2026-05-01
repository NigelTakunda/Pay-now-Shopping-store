using PayNowStore.Api.Services;

namespace PayNowStore.Api.Tests.Services;

public class PaynowServiceTests
{
    [Fact]
    public void GenerateHash_MatchesOfficialDocumentationSample()
    {
        var values = new Dictionary<string, string>
        {
            ["id"] = "1201",
            ["reference"] = "TEST REF",
            ["amount"] = "99.99",
            ["additionalinfo"] = "A test ticket transaction",
            ["returnurl"] = "http://www.google.com/search?q=returnurl",
            ["resulturl"] = "http://www.google.com/search?q=resulturl",
            ["status"] = "Message"
        };

        var result = PaynowService.GenerateHash(values, "3e9fed89-60e1-4ce5-ab6e-6b1eb2d4f977");

        Assert.Equal("2A033FC38798D913D42ECB786B9B19645ADEDBDE788862032F1BD82CF3B92DEF84F316385D5B40DBB35F1A4FD7D5BFE73835174136463CDD48C9366B0749C689", result);
    }
}
