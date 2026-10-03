using SIGNL4.Core.Models;
using System.Text;
using System.Text.Json;

namespace SIGNL4.Core.Services
{
    /// <summary>
    /// Sends alerts to a SIGNL4 team through the team's inbound webhook. An unofficial client,
    /// not made or supported by Derdack, the maker of SIGNL4.
    /// </summary>
    public class AlertService
    {
        // One client for the life of the process. Its connections are replaced every two
        // minutes, so the webhook host's address is looked up again even when alerts never
        // pause long enough for a connection to go idle.
        private static readonly HttpClient _httpClient = new(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        });

        /// <summary>
        /// Posts one alert to a SIGNL4 webhook as JSON and completes when the webhook answers
        /// with a status from 200 to 299. Every call shares one static <see cref="HttpClient"/>,
        /// so the call times out after 100 seconds and cannot be cancelled. The returned task
        /// does not need the caller's synchronization context, so blocking on it does not
        /// deadlock.
        /// </summary>
        /// <param name="webhookUrl">
        /// The team's webhook URL, <c>https://connect.signl4.com/webhook/{team-secret}</c>.
        /// It holds the team secret, so keep it out of source code and logs. It must be an
        /// absolute <c>https</c> URL; plain <c>http</c> is accepted only for the local machine
        /// (a loopback address or <c>localhost</c>), such as a test server.
        /// </param>
        /// <param name="title">The alert's title, sent as the <c>title</c> field.</param>
        /// <param name="description">The alert's text, sent as the <c>message</c> field.</param>
        /// <param name="severity">
        /// Sent in lower case as the <c>severity</c> field. SIGNL4 documents no severity field,
        /// so it arrives as an ordinary alert parameter.
        /// </param>
        /// <param name="category">
        /// Sent as the <c>X-S4-Service</c> control parameter, which puts the alert in the
        /// SIGNL4 category with that name.
        /// </param>
        /// <param name="details">
        /// Extra name and value pairs, sent in order as the <c>details</c> field, an array of
        /// <c>{"Key": ..., "Value": ...}</c> objects. Null sends an empty array.
        /// </param>
        /// <returns>A task that completes once the webhook has accepted the alert.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="webhookUrl"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="webhookUrl"/> is empty, is not an absolute URL, or is not https and
        /// not http to the local machine. Nothing is sent.
        /// </exception>
        /// <exception cref="NullReferenceException"><paramref name="severity"/> is null.</exception>
        /// <exception cref="HttpRequestException">
        /// The webhook cannot be reached, answers with a status outside 200 to 299, or answers
        /// with a redirect (301, 302 or 303) that HttpClient follows with a GET, which drops the
        /// alert.
        /// </exception>
        /// <exception cref="TaskCanceledException">The webhook did not answer within 100 seconds.</exception>
        public static async Task SendAlertAsync(string webhookUrl, string title, string description, string severity = "low", string category = "Default", List<KeyValuePair<string, string>>? details = null)
        {
            var webhook = CheckWebhookUrl(webhookUrl);

            var payload = new AlertPayload
            {
                Title = title,
                Description = description,
                Severity = severity.ToLowerInvariant(),
                Category = category,
                DetailKeyValuePairs = details ?? []
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // ConfigureAwait(false): a caller that blocks on the task from a single-threaded
            // context, such as a UI thread, would otherwise deadlock.
            using var response = await _httpClient.PostAsync(webhook, content).ConfigureAwait(false);

            // A 301, 302 or 303 makes HttpClient follow with a GET and no body, so a success
            // after one means the alert was dropped on the way.
            if (response.RequestMessage?.Method != HttpMethod.Post)
            {
                throw new HttpRequestException(
                    "The webhook redirected the alert, and HttpClient followed the redirect with a GET " +
                    "that has no body, so no alert was raised. Use the webhook URL SIGNL4 gives you.");
            }

            response.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// The webhook URL as a <see cref="Uri"/>, if it may carry the team secret: https, or http
        /// to the local machine only. The messages leave the URL out, since it holds the secret.
        /// </summary>
        private static Uri CheckWebhookUrl(string webhookUrl)
        {
            ArgumentNullException.ThrowIfNull(webhookUrl);
            if (string.IsNullOrWhiteSpace(webhookUrl))
            {
                throw new ArgumentException("The webhook URL is empty.", nameof(webhookUrl));
            }
            if (!Uri.TryCreate(webhookUrl, UriKind.Absolute, out var uri))
            {
                throw new ArgumentException("The webhook URL is not an absolute URL.", nameof(webhookUrl));
            }
            if (uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback))
            {
                return uri;
            }
            throw new ArgumentException(
                "The webhook URL must use https, so that the team secret in it is not sent in clear text. " +
                "Plain http is accepted only for the local machine, such as a test server on 127.0.0.1.",
                nameof(webhookUrl));
        }
    }
}
