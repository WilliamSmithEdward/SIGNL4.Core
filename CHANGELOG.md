# Changelog

Each release's notes. The Publish workflow takes the section for the
version it releases as the GitHub release's body, so a section is written
here before the version is tagged.

The sections up to 1.0.3 were gathered from the nuget.org version history,
with the UTC date the nuget.org catalog records for each upload. Neither
nuget.org nor the READMEs carried release notes for them. All four versions
are listed on nuget.org and target net9.0.

## [2.0.1] - 2026-10-04

* The NuGet package now embeds the root GitHub `README.md`, including its badges, as its only README. The OpenSSF Scorecard badge is served through `img.shields.io`, which NuGet supports.
* CI and Publish verify that the packaged README exactly matches the root file.
* No library API or runtime behavior changes.

## [2.0.0] - 2026-10-02

The webhook URL must use https, so the team secret in it is never sent in clear text, and a redirect that drops an alert throws instead of passing for success. Blocking on the returned task no longer deadlocks on a UI thread, the shared HttpClient picks up a change to the webhook host's address, a null severity throws `ArgumentNullException`, and a new overload takes a `CancellationToken`. Two of the fixes change what callers see, hence the major version. The library has no package dependencies.

### Breaking changes

* `SendAlertAsync` refuses a webhook URL that is not `https`, except `http` to the local machine (a loopback address or `localhost`), with `ArgumentException` naming `webhookUrl`, before anything is sent. 1.0.3 posted to an `http://` URL on any host, sending the team secret and the alert in clear text. A malformed URL throws `ArgumentException` too, where 1.0.3 threw `InvalidOperationException` (a blank or relative URL), `NotSupportedException` (a scheme such as `ftp`) or `UriFormatException`. A null URL throws `ArgumentNullException`, a subclass of the `ArgumentException` it threw before.
* A `301`, `302` or `303` answer, which HttpClient follows with a `GET` that has no body, throws `HttpRequestException`. 1.0.3 completed as if the alert had been raised, though none was. A `307` or `308` still delivers the alert.

### Fixes

* `SendAlertAsync` awaited without `ConfigureAwait(false)`, so blocking on its task (`.Wait()`, `.Result`) from a WinForms or WPF UI thread or classic ASP.NET deadlocked.
* The shared HttpClient kept each connection for as long as calls came within a minute of each other, so a process that alerted that often never looked up the webhook host's address again. Connections are now replaced every two minutes.
* A null `severity` threw `NullReferenceException`. It throws `ArgumentNullException` naming `severity`, before anything is sent.
* The READMEs ended inside an unclosed code block after the method signature, described exception formatting the library has not had since 1.0.2, and named .NET 6 for a package that targeted net9.0 only.

### Additions

* `SendAlertAsync(string webhookUrl, string title, string description, string severity, string category, List<KeyValuePair<string, string>>? details, CancellationToken cancellationToken)` can be cancelled, which also gives a call a shorter timeout than HttpClient's 100 seconds. The original overload is unchanged.
* The package targets net8.0, net9.0 and net10.0, where 1.0.3 targeted net9.0 only. .NET 8 and .NET 9 leave Microsoft support on 2026-11-10.
* The package carries XML documentation, and its metadata names the author, the repository and the library as an unofficial SIGNL4 client.
* The READMEs are rewritten against the code, with samples that compile and run, the exact JSON the library sends and how it compares with SIGNL4's webhook documentation. SECURITY.md says what the library reaches and how to keep the webhook URL safe.
* The package is built in CI from the tagged commit, tested on all three frameworks, scanned for vulnerabilities and malware, and published through nuget.org trusted publishing. The GitHub release carries the package's signed build provenance.

## [1.0.3] - 2025-05-12

No notes were recorded.

## [1.0.2] - 2025-05-12

No notes were recorded.

## [1.0.1] - 2025-05-12

No notes were recorded.

## [1.0.0] - 2025-05-12

No notes were recorded.
