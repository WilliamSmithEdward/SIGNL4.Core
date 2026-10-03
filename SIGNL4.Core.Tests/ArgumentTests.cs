using SIGNL4.Core.Services;

namespace SIGNL4.Core.Tests;

// The arguments other than the URL. Every request goes to FakeWebhook on 127.0.0.1.
public class ArgumentTests
{
    [Fact]
    public async Task A_null_severity_throws_ArgumentNullException_before_anything_is_sent()
    {
        using var webhook = new FakeWebhook();

        var error = await Assert.ThrowsAsync<ArgumentNullException>(
            () => AlertService.SendAlertAsync(webhook.Url, "t", "d", severity: null!));

        Assert.Equal("severity", error.ParamName);
        Assert.Empty(webhook.Requests);
    }

    [Fact]
    public async Task A_null_title_description_or_category_is_sent_as_json_null()
    {
        using var webhook = new FakeWebhook();

        await AlertService.SendAlertAsync(webhook.Url, null!, null!, category: null!);

        Assert.Equal(
            """{"title":null,"message":null,"severity":"low","X-S4-Service":null,"details":[]}""",
            Assert.Single(webhook.Requests).Body);
    }
}
