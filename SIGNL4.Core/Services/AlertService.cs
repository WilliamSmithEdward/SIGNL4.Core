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
        private static readonly HttpClient _httpClient = new();

        /// <summary>
        /// Posts one alert to a SIGNL4 webhook as JSON and completes when the webhook answers
        /// with a status from 200 to 299. Every call shares one static <see cref="HttpClient"/>
        /// with default settings, so the call times out after 100 seconds and cannot be
        /// cancelled.
        /// </summary>
        /// <param name="webhookUrl">
        /// The team's webhook URL, <c>https://connect.signl4.com/webhook/{team-secret}</c>.
        /// It holds the team secret, so keep it out of source code and logs. An <c>http</c>
        /// URL is accepted too, and then the secret and the alert cross the network in clear
        /// text.
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
        /// <exception cref="ArgumentException"><paramref name="webhookUrl"/> is null or empty.</exception>
        /// <exception cref="InvalidOperationException"><paramref name="webhookUrl"/> is not an absolute URL.</exception>
        /// <exception cref="NotSupportedException"><paramref name="webhookUrl"/> uses a scheme other than http or https.</exception>
        /// <exception cref="UriFormatException"><paramref name="webhookUrl"/> cannot be parsed.</exception>
        /// <exception cref="NullReferenceException"><paramref name="severity"/> is null.</exception>
        /// <exception cref="HttpRequestException">
        /// The webhook cannot be reached, or answers with a status outside 200 to 299.
        /// </exception>
        /// <exception cref="TaskCanceledException">The webhook did not answer within 100 seconds.</exception>
        public static async Task SendAlertAsync(string webhookUrl, string title, string description, string severity = "low", string category = "Default", List<KeyValuePair<string, string>>? details = null)
        {
            if (string.IsNullOrEmpty(webhookUrl))
            {
                throw new ArgumentException("Webhook URL cannot be null or empty.", nameof(webhookUrl));
            }

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

            using var response = await _httpClient.PostAsync(webhookUrl, content);

            response.EnsureSuccessStatusCode();
        }
    }
}
