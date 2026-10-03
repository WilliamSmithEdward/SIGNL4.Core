# SIGNL4.Core

[![NuGet version](https://img.shields.io/nuget/v/SIGNL4.Core)](https://www.nuget.org/packages/SIGNL4.Core)
[![Downloads](https://img.shields.io/nuget/dt/SIGNL4.Core)](https://www.nuget.org/packages/SIGNL4.Core)
[![CI](https://github.com/WilliamSmithEdward/SIGNL4.Core/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/WilliamSmithEdward/SIGNL4.Core/actions/workflows/ci.yml)
[![Security](https://github.com/WilliamSmithEdward/SIGNL4.Core/actions/workflows/security.yml/badge.svg?branch=main)](https://github.com/WilliamSmithEdward/SIGNL4.Core/actions/workflows/security.yml)
[![Malware scan](https://github.com/WilliamSmithEdward/SIGNL4.Core/actions/workflows/malware-scan.yml/badge.svg?branch=main)](https://github.com/WilliamSmithEdward/SIGNL4.Core/actions/workflows/malware-scan.yml)
[![OpenSSF Scorecard](https://api.scorecard.dev/projects/github.com/WilliamSmithEdward/SIGNL4.Core/badge)](https://scorecard.dev/viewer/?uri=github.com/WilliamSmithEdward/SIGNL4.Core)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue)](https://github.com/WilliamSmithEdward/SIGNL4.Core/blob/main/LICENSE.txt)

SIGNL4.Core sends an alert to a SIGNL4 team through the team's inbound webhook. It is an unofficial client: SIGNL4 is a product of Derdack, and this library is not made, endorsed or supported by Derdack.

```
dotnet add package SIGNL4.Core
```

The package targets net8.0, net9.0 and net10.0 and has no package dependencies. .NET 8 and .NET 9 leave Microsoft support on 2026-11-10.

---

## Send an alert

```csharp
using SIGNL4.Core.Services;

// The webhook URL holds the team secret. Read it from configuration or a
// secret store; never write it into source code.
string webhookUrl = Environment.GetEnvironmentVariable("SIGNL4_WEBHOOK_URL")
    ?? throw new InvalidOperationException("Set SIGNL4_WEBHOOK_URL.");

await AlertService.SendAlertAsync(
    webhookUrl,
    title: "Disk almost full",
    description: "Volume D: on web-01 has 2% free.",
    severity: "High",
    category: "Storage",
    details:
    [
        new("Host", "web-01"),
        new("Free space", "2%"),
    ]);
```

`SendAlertAsync` posts one JSON object to the URL and completes when the webhook answers with a success status. Only `webhookUrl`, `title` and `description` are required:

```csharp
using SIGNL4.Core.Services;

string webhookUrl = Environment.GetEnvironmentVariable("SIGNL4_WEBHOOK_URL")
    ?? throw new InvalidOperationException("Set SIGNL4_WEBHOOK_URL.");

await AlertService.SendAlertAsync(webhookUrl, "Nightly import failed", "The 02:00 import stopped at row 1,204.");
```

To cancel a call, or give up sooner than HttpClient's 100 seconds, pass a `CancellationToken`. That overload takes every argument:

```csharp
using SIGNL4.Core.Services;

string webhookUrl = Environment.GetEnvironmentVariable("SIGNL4_WEBHOOK_URL")
    ?? throw new InvalidOperationException("Set SIGNL4_WEBHOOK_URL.");

// Give up after 10 seconds.
using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

await AlertService.SendAlertAsync(
    webhookUrl, "Nightly import failed", "The 02:00 import stopped at row 1,204.",
    "high", "Imports", null, timeout.Token);
```

A token cancelled before the call sends nothing. One cancelled after the request has gone ends the call, but SIGNL4 may still raise the alert.

### What is sent

The first sample posts this body, with `Content-Type: application/json; charset=utf-8`:

```json
{"title":"Disk almost full","message":"Volume D: on web-01 has 2% free.","severity":"high","X-S4-Service":"Storage","details":[{"Key":"Host","Value":"web-01"},{"Key":"Free space","Value":"2%"}]}
```

| Parameter | JSON field | What the library does |
|---|---|---|
| `webhookUrl` | none | Posts to this URL, which must be `https`. SIGNL4's form is `https://connect.signl4.com/webhook/{team-secret}`. |
| `title` | `title` | Sends it as given. |
| `description` | `message` | Sends it as given. |
| `severity` | `severity` | Sends it in lower case. Default `"low"`. |
| `category` | `X-S4-Service` | Sends it as given. Default `"Default"`. |
| `details` | `details` | Sends the pairs as an array of `{"Key": ..., "Value": ...}` objects, in order. Default: an empty array. |

Text is encoded by System.Text.Json, which writes a line break as `\n` and quotes, `<`, `>`, `&` and every non-ASCII character as `\uXXXX` escapes; a JSON reader decodes them back to the original text. A null `title`, `description` or `category` is sent as JSON `null`.

### How SIGNL4 reads it

From SIGNL4's [webhook documentation](https://docs.signl4.com/integrations/webhook/) and [code samples](https://docs.signl4.com/samples/code-samples/), as read on 2026-10-02:

- SIGNL4 documents `Title` (or `subject`) and `Message` (or `body`), capitalized. The library sends `title` and `message` in lower case. The documentation does not say whether field names are case sensitive.
- `X-S4-Service` is a documented control parameter: it puts the alert in the category with that name. The library always sends one, `Default` unless you name another category.
- SIGNL4 documents no severity or priority field. `severity` arrives as an ordinary alert parameter and does not change how SIGNL4 alerts the team.
- SIGNL4's examples show extra parameters as top-level fields holding text. The documentation does not say how SIGNL4 shows a field holding an array of objects, such as `details`.
- The library sends none of the other control parameters (`X-S4-AlertingScenario`, `X-S4-ExternalID`, `X-S4-Status`, `X-S4-Location`, `X-S4-Filtering`), so it raises new alerts but cannot acknowledge or resolve one.
- The code samples treat `201 Created` as success. The library accepts any status from 200 to 299.

### Errors

- `ArgumentNullException` when `webhookUrl` is null, and `ArgumentException` when it is empty, is not an absolute URL, or is not `https`. Plain `http` is accepted only for the local machine (a loopback address or `localhost`), such as a test server. Nothing is sent, and the message leaves the URL out.
- `ArgumentNullException` when `severity` is null. Nothing is sent.
- `HttpRequestException` when the webhook cannot be reached, answers with a status outside 200 to 299, or answers with a redirect (`301`, `302` or `303`) that HttpClient follows with a `GET`, which drops the alert. The message names the status code, or the host and port, never the path, so it does not hold the team secret.
- `OperationCanceledException` when the token is cancelled, and `TaskCanceledException` (a subclass) when no answer arrives within 100 seconds, HttpClient's default timeout.

Every call goes through one static `HttpClient`, created on first use and never disposed. It uses the system proxy, checks the server's certificate, follows redirects (a `307` or `308` keeps the alert), and replaces its connections every two minutes, so a change to the webhook host's address is seen. Blocking on the returned task (`.Wait()`, `.Result`) does not deadlock, even on a UI thread. Its proxy and handler cannot be changed.

---

## Security

### The webhook URL is a secret

Anyone holding the URL can raise alerts for the team, so treat it as a password: keep it in configuration or a secret store, out of source control and out of logs. The library's exception messages leave it out. HTTP tracing or logging in your application, such as OpenTelemetry's HttpClient instrumentation, can record the full request URL, secret included.

### Use the https URL

The library sends the alert only to an `https://` URL, or over `http` to the local machine, so the team secret and the alert do not cross the network in clear text. Version 1.0.3 also posted to an `http://` URL on any host; use the `https://` URL SIGNL4 gives you.

### What goes into an alert

The alert carries the text you pass, unchanged, and the library sets no size limit. SIGNL4 shows it on the team's phones. Leave out passwords, connection strings and personal data. An exception's message or stack trace can hold file paths, server names and sometimes secrets, so check what it holds before you put it into `description` or `details`.

---

## License

[MIT](https://github.com/WilliamSmithEdward/SIGNL4.Core/blob/main/LICENSE.txt).
