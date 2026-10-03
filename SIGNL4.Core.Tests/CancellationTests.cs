using System.Diagnostics;
using SIGNL4.Core.Services;

namespace SIGNL4.Core.Tests;

// The overload that takes a CancellationToken, which also sets a shorter timeout than
// HttpClient's 100 seconds. Every request goes to FakeWebhook on 127.0.0.1.
public class CancellationTests
{
    [Fact]
    public async Task A_cancelled_call_ends_without_waiting_for_the_webhook()
    {
        using var answer = new ManualResetEventSlim();
        using var webhook = new FakeWebhook(_ => { answer.Wait(TimeSpan.FromSeconds(30)); return Reply.Created; });
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var clock = Stopwatch.StartNew();

        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => AlertService.SendAlertAsync(
                webhook.Url, "t", "d", "low", "Default", null, timeout.Token));
            Assert.True(clock.Elapsed < TimeSpan.FromSeconds(10), $"the call took {clock.Elapsed}");
        }
        finally
        {
            answer.Set();
        }
    }

    [Fact]
    public async Task A_token_cancelled_before_the_call_sends_nothing()
    {
        using var webhook = new FakeWebhook();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => AlertService.SendAlertAsync(
            webhook.Url, "t", "d", "low", "Default", null, new CancellationToken(canceled: true)));

        Assert.Empty(webhook.Requests);
    }

    [Fact]
    public async Task The_overload_sends_the_same_alert_and_checks_the_same_arguments()
    {
        using var webhook = new FakeWebhook();

        await AlertService.SendAlertAsync(webhook.Url, "Disk almost full", "d", "High", "Storage",
            [new("Host", "web-01")], CancellationToken.None);

        Assert.Equal(
            """{"title":"Disk almost full","message":"d","severity":"high","X-S4-Service":"Storage","details":[{"Key":"Host","Value":"web-01"}]}""",
            Assert.Single(webhook.Requests).Body);
        await Assert.ThrowsAsync<ArgumentException>(() => AlertService.SendAlertAsync(
            "http://signl4-test.invalid/webhook/" + FakeWebhook.Secret, "t", "d", "low", "Default", null, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => AlertService.SendAlertAsync(
            webhook.Url, "t", "d", null!, "Default", null, CancellationToken.None));
    }
}
