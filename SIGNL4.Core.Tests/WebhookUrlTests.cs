using SIGNL4.Core.Services;

namespace SIGNL4.Core.Tests;

// Which webhook URLs SendAlertAsync takes. The team secret is in the URL's path, so the library
// sends it only over https, or over http to the local machine. Hosts under .invalid never
// resolve, so a refused URL could not have reached anything even if it were sent.
public class WebhookUrlTests
{
    [Theory]
    [InlineData("http://signl4-test.invalid/webhook/" + FakeWebhook.Secret)]
    [InlineData("HTTP://Signl4-Test.invalid/webhook/" + FakeWebhook.Secret)]
    public async Task An_http_url_to_another_machine_is_refused_before_anything_is_sent(string url)
    {
        var error = await Assert.ThrowsAsync<ArgumentException>(() => AlertService.SendAlertAsync(url, "t", "d"));

        Assert.Equal("webhookUrl", error.ParamName);
        Assert.Contains("https", error.Message);
        Assert.DoesNotContain(FakeWebhook.Secret, error.ToString());
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("webhook/" + FakeWebhook.Secret)]
    [InlineData("ftp://signl4-test.invalid/webhook/" + FakeWebhook.Secret)]
    [InlineData("https://signl4 test.invalid/webhook/" + FakeWebhook.Secret)]
    public async Task A_url_that_is_not_an_absolute_https_url_throws_ArgumentException(string url)
    {
        var error = await Assert.ThrowsAsync<ArgumentException>(() => AlertService.SendAlertAsync(url, "t", "d"));

        Assert.Equal("webhookUrl", error.ParamName);
        Assert.DoesNotContain(FakeWebhook.Secret, error.ToString());
    }

    [Fact]
    public async Task A_null_url_throws_ArgumentNullException()
    {
        var error = await Assert.ThrowsAsync<ArgumentNullException>(() => AlertService.SendAlertAsync(null!, "t", "d"));

        Assert.Equal("webhookUrl", error.ParamName);
    }

    [Fact]
    public async Task An_https_url_passes_the_check_and_is_sent()
    {
        // The name never resolves, so the request fails in DNS: past the URL check, and
        // nowhere near a real server.
        var error = await Assert.ThrowsAsync<HttpRequestException>(
            () => AlertService.SendAlertAsync("https://signl4-test.invalid/webhook/" + FakeWebhook.Secret, "t", "d"));

        Assert.DoesNotContain(FakeWebhook.Secret, error.ToString());
    }

    [Fact]
    public async Task An_http_url_to_the_loopback_address_is_sent()
    {
        using var webhook = new FakeWebhook();

        await AlertService.SendAlertAsync(webhook.Url, "t", "d");

        Assert.Single(webhook.Requests);
    }
}
