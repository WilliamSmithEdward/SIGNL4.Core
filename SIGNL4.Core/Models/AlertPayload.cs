using System.Text.Json.Serialization;

namespace SIGNL4.Core.Models
{
    class AlertPayload
    {
        [JsonPropertyName("title")]
        public required string Title { get; set; }
        
        [JsonPropertyName("message")]
        public required string Description { get; set; }

        [JsonPropertyName("severity")]
        public string Severity { get; set; } = "low";

        [JsonPropertyName("X-S4-Service")]
        public string Category { get; set; } = "Default";

        [JsonPropertyName("details")]
        public List<KeyValuePair<string, string>> DetailKeyValuePairs { get; set; } = [];
    }
}
