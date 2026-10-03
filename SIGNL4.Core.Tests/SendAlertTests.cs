using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using SIGNL4.Core.Services;

namespace SIGNL4.Core.Tests;

// What SendAlertAsync sends and how it reports a failure. Every request goes to FakeWebhook on
// 127.0.0.1; nothing reaches SIGNL4.
public class SendAlertTests
{
    [Fact]
    public async Task Posts_the_readme_sample_as_the_json_the_readme_shows()
    {
        using var webhook = new FakeWebhook();

        await AlertService.SendAlertAsync(
            webhook.Url,
            title: "Disk almost full",
            description: "Volume D: on web-01 has 2% free.",
            severity: "High",
            category: "Storage",
            details:
            [
                new("Host", "web-01"),
                new("Free space", "2%"),
            ]);

        var request = Assert.Single(webhook.Requests);
        Assert.Equal("POST", request.Method);
        Assert.Equal($"/webhook/{FakeWebhook.Secret}", request.Path);
        Assert.Equal("application/json; charset=utf-8", request.Headers["Content-Type"]);
        Assert.Equal(
            """{"title":"Disk almost full","message":"Volume D: on web-01 has 2% free.","severity":"high","X-S4-Service":"Storage","details":[{"Key":"Host","Value":"web-01"},{"Key":"Free space","Value":"2%"}]}""",
            request.Body);
    }

    [Fact]
    public async Task Sends_the_defaults_when_only_the_required_arguments_are_given()
    {
        using var webhook = new FakeWebhook();

        await AlertService.SendAlertAsync(webhook.Url, "Nightly import failed", "The 02:00 import stopped.");

        Assert.Equal(
            """{"title":"Nightly import failed","message":"The 02:00 import stopped.","severity":"low","X-S4-Service":"Default","details":[]}""",
            Assert.Single(webhook.Requests).Body);
    }

    [Fact]
    public async Task Null_details_send_an_empty_array_and_details_keep_their_order()
    {
        using var webhook = new FakeWebhook();

        await AlertService.SendAlertAsync(webhook.Url, "t", "d", details: null);
        await AlertService.SendAlertAsync(webhook.Url, "t", "d", details: [new("b", "2"), new("a", "1"), new("b", "3")]);

        var requests = webhook.Requests;
        Assert.Equal(2, requests.Count);
        Assert.Contains("\"details\":[]", requests[0].Body);
        Assert.Contains("""[{"Key":"b","Value":"2"},{"Key":"a","Value":"1"},{"Key":"b","Value":"3"}]""", requests[1].Body);
    }

    [Fact]
    public async Task Caller_text_is_json_encoded_and_reads_back_unchanged()
    {
        string accented = "caf" + (char)0xE9;
        string title = "Quote \" backslash \\ <b>tag</b> & " + accented;
        string description = "Line one\nLine two\r\n\ttabbed";
        using var webhook = new FakeWebhook();

        await AlertService.SendAlertAsync(webhook.Url, title, description, category: "Ops & <Infra>",
            details: [new("Path", "C:\\logs\\app.log"), new("Note", "\"quoted\"")]);

        string body = Assert.Single(webhook.Requests).Body;
        Assert.DoesNotContain("<b>", body);
        Assert.DoesNotContain(accented, body);
        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        Assert.Equal(title, root.GetProperty("title").GetString());
        Assert.Equal(description, root.GetProperty("message").GetString());
        Assert.Equal("Ops & <Infra>", root.GetProperty("X-S4-Service").GetString());
        var details = root.GetProperty("details");
        Assert.Equal("C:\\logs\\app.log", details[0].GetProperty("Value").GetString());
        Assert.Equal("\"quoted\"", details[1].GetProperty("Value").GetString());
    }

    [Fact]
    public async Task Severity_is_sent_in_lower_case_whatever_the_culture()
    {
        using var webhook = new FakeWebhook();
        var culture = CultureInfo.CurrentCulture;
        try
        {
            // Turkish lower-cases a capital I to a dotless i; the library lower-cases invariantly.
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            await AlertService.SendAlertAsync(webhook.Url, "t", "d", severity: "CRITICAL");
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }

        Assert.Contains("\"severity\":\"critical\"", Assert.Single(webhook.Requests).Body);
    }

    [Fact]
    public async Task An_empty_url_is_refused_before_anything_is_sent()
    {
        var error = await Assert.ThrowsAsync<ArgumentException>(() => AlertService.SendAlertAsync("", "t", "d"));

        Assert.Equal("webhookUrl", error.ParamName);
    }

    [Theory]
    [InlineData(404)]
    [InlineData(500)]
    public async Task A_status_outside_200_to_299_throws_without_the_secret_in_the_message(int status)
    {
        using var webhook = new FakeWebhook(_ => new Reply(status));

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => AlertService.SendAlertAsync(webhook.Url, "t", "d"));

        Assert.Equal((HttpStatusCode)status, error.StatusCode);
        Assert.DoesNotContain(FakeWebhook.Secret, error.ToString());
        Assert.Single(webhook.Requests);
    }

    [Theory]
    [InlineData(200)]
    [InlineData(202)]
    [InlineData(204)]
    public async Task Any_status_from_200_to_299_is_success(int status)
    {
        using var webhook = new FakeWebhook(_ => new Reply(status));

        await AlertService.SendAlertAsync(webhook.Url, "t", "d");

        Assert.Single(webhook.Requests);
    }

    [Fact]
    public async Task A_webhook_that_cannot_be_reached_throws_without_the_secret_in_the_message()
    {
        var closed = new TcpListener(IPAddress.Loopback, 0);
        closed.Start();
        int port = ((IPEndPoint)closed.LocalEndpoint).Port;
        closed.Stop();

        var error = await Assert.ThrowsAsync<HttpRequestException>(
            () => AlertService.SendAlertAsync($"http://127.0.0.1:{port}/webhook/{FakeWebhook.Secret}", "t", "d"));

        Assert.DoesNotContain(FakeWebhook.Secret, error.ToString());
    }
}
