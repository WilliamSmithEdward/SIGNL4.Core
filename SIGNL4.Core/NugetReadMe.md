# SIGNL4 AlertService

The SIGNL4.Core.Services.AlertService class provides a simple, extensible way to send alerts to your SIGNL4 team using a webhook. It supports optional severity, category tagging, and exception formatting.

---

## Requirements

- .NET 6.0 or later
- Reference to SIGNL4.Core in your project
- A valid SIGNL4 webhook URL

---

## Usage

### Method Signature

```csharp
public static async Task SendAlertAsync(
    string webhookUrl,
    string title,
    string description,
    string severity = "low",
    string category = "Default",
    DetailKeyValuePairs = details ?? []
)