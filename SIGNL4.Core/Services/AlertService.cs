using SIGNL4.Core.Models;
using System.Text;
using System.Text.Json;

namespace SIGNL4.Core.Services
{
    public class AlertService
    {
        private static readonly HttpClient _httpClient = new();

        public static async Task SendAlertAsync(string webhookUrl, string title, string details, string severity = "low", string category = "Default", List<Exception>? exceptions = null)
        {
            if (string.IsNullOrEmpty(webhookUrl))
            {
                throw new ArgumentException("Webhook URL cannot be null or empty.", nameof(webhookUrl));
            }

            var formattedDetails = new StringBuilder(details);

            if (exceptions is { Count: > 0 })
            {
                formattedDetails.AppendLine();
                formattedDetails.AppendLine("Exceptions:");

                foreach (var ex in exceptions)
                {
                    formattedDetails.AppendLine($"--- {ex.GetType().Name} ---");
                    formattedDetails.AppendLine(ex.Message);
                    formattedDetails.AppendLine(ex.StackTrace);
                }
            }

            var payload = new AlertPayload
            {
                Title = title,
                Details = formattedDetails.ToString(),
                Severity = severity.ToLowerInvariant(),
                Category = category
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await _httpClient.PostAsync(webhookUrl, content);

            response.EnsureSuccessStatusCode();
        }
    }
}
