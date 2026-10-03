using System.Net;
using System.Reflection;
using SIGNL4.Core.Services;

namespace SIGNL4.Core.Tests;

// How the alert travels: what a redirect does to it, whether a caller that blocks can deadlock,
// and how long the shared client keeps a connection. Every request goes to FakeWebhook on
// 127.0.0.1.
public class DeliveryTests
{
    private static Reply RedirectPostsTo(ReceivedRequest request, int status, string target) =>
        request.Method == "POST" && request.Path.StartsWith("/webhook/")
            ? new Reply(status, new Dictionary<string, string> { ["Location"] = target })
            : new Reply(200);

    [Theory]
    [InlineData(301)]
    [InlineData(302)]
    [InlineData(303)]
    public async Task A_redirect_that_drops_the_body_throws_instead_of_reporting_success(int status)
    {
        using var webhook = new FakeWebhook(r => RedirectPostsTo(r, status, "/moved/" + FakeWebhook.Secret));

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => AlertService.SendAlertAsync(webhook.Url, "t", "d"));

        Assert.DoesNotContain(FakeWebhook.Secret, error.ToString());
        // HttpClient followed the redirect with a GET and no body: the alert went nowhere.
        Assert.Equal(["POST", "GET"], webhook.Requests.Select(r => r.Method));
        Assert.Equal("", webhook.Requests[1].Body);
    }

    [Theory]
    [InlineData(307)]
    [InlineData(308)]
    public async Task A_redirect_that_keeps_the_post_delivers_the_alert(int status)
    {
        using var webhook = new FakeWebhook(r => RedirectPostsTo(r, status, "/moved/" + FakeWebhook.Secret));

        await AlertService.SendAlertAsync(webhook.Url, "Disk almost full", "d");

        Assert.Equal(["POST", "POST"], webhook.Requests.Select(r => r.Method));
        Assert.Contains("Disk almost full", webhook.Requests[1].Body);
    }

    [Fact]
    public void Blocking_on_the_task_under_a_single_threaded_context_does_not_deadlock()
    {
        using var webhook = new FakeWebhook();
        bool completed = false;
        Exception? failure = null;

        // A UI thread that blocks on the task: work posted to its context never runs until the
        // wait ends, so a continuation that needs the context waits forever.
        var thread = new Thread(() =>
        {
            try
            {
                SynchronizationContext.SetSynchronizationContext(new BlockedContext());
                completed = AlertService.SendAlertAsync(webhook.Url, "t", "d").Wait(TimeSpan.FromSeconds(10));
            }
            catch (Exception e)
            {
                failure = e;
            }
        });
        thread.Start();
        thread.Join();

        Assert.Null(failure);
        Assert.True(completed, "SendAlertAsync did not complete while its caller blocked a single-threaded context");
        Assert.Single(webhook.Requests);
    }

    [Fact]
    public void The_shared_client_replaces_its_connections_so_a_dns_change_is_seen()
    {
        var clientField = typeof(AlertService)
            .GetFields(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(f => f.FieldType == typeof(HttpClient));
        var client = (HttpClient)clientField.GetValue(null)!;
        var handlerField = typeof(HttpMessageInvoker).GetField("_handler", BindingFlags.NonPublic | BindingFlags.Instance)!;

        var handler = Assert.IsType<SocketsHttpHandler>(handlerField.GetValue(client));

        Assert.NotEqual(Timeout.InfiniteTimeSpan, handler.PooledConnectionLifetime);
        Assert.InRange(handler.PooledConnectionLifetime, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(5));
    }

    private sealed class BlockedContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state) { }

        public override void Send(SendOrPostCallback d, object? state) { }
    }
}
