using SIGNL4.Core.Models;
using System.Text;
using System.Text.Json;

namespace SIGNL4.Core.Services
{
    public class AlertService
    {
        private static readonly HttpClient _httpClient = new();

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
