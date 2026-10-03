using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SIGNL4.Core.Tests;

/// <summary>One request the fake webhook received.</summary>
internal sealed record ReceivedRequest(string Method, string Path, IReadOnlyDictionary<string, string> Headers, string Body);

/// <summary>What the fake webhook answers: a status and any extra headers, such as Location.</summary>
internal sealed record Reply(int Status, IReadOnlyDictionary<string, string>? Headers = null)
{
    /// <summary>What SIGNL4's code samples treat as success.</summary>
    public static Reply Created { get; } = new(201);
}

/// <summary>
/// A stand-in for a SIGNL4 webhook, in this process, on 127.0.0.1 and a port the system picks.
/// It keeps every request and answers each with the reply its function gives (201 Created by
/// default), one request per connection. No test reaches the network or SIGNL4.
/// </summary>
internal sealed class FakeWebhook : IDisposable
{
    /// <summary>The team secret in the fake's URL: a placeholder, never a real one.</summary>
    public const string Secret = "test-team-secret-0000";

    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly Func<ReceivedRequest, Reply> _respond;
    private readonly List<ReceivedRequest> _requests = [];
    private readonly Task _accepting;

    public FakeWebhook(Func<ReceivedRequest, Reply>? respond = null)
    {
        _respond = respond ?? (_ => Reply.Created);
        _listener.Start();
        _accepting = Task.Run(AcceptAsync);
    }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    /// <summary>The webhook URL, in SIGNL4's form, over http on the loopback address.</summary>
    public string Url => $"http://127.0.0.1:{Port}/webhook/{Secret}";

    public IReadOnlyList<ReceivedRequest> Requests { get { lock (_requests) return [.. _requests]; } }

    public void Dispose()
    {
        _listener.Stop();
        try { _accepting.Wait(TimeSpan.FromSeconds(5)); } catch (AggregateException) { }
    }

    private async Task AcceptAsync()
    {
        while (true)
        {
            TcpClient client;
            try { client = await _listener.AcceptTcpClientAsync(); }
            catch (Exception e) when (e is SocketException or ObjectDisposedException) { return; }
            _ = Task.Run(() => ServeAsync(client));
        }
    }

    private async Task ServeAsync(TcpClient client)
    {
        using (client)
        {
            try
            {
                var stream = client.GetStream();
                var request = await ReadRequestAsync(stream);
                if (request is null) return;
                lock (_requests) _requests.Add(request);
                var reply = _respond(request);
                var head = new StringBuilder($"HTTP/1.1 {reply.Status} Fake\r\nContent-Length: 0\r\nConnection: close\r\n");
                foreach (var (name, value) in reply.Headers ?? new Dictionary<string, string>())
                {
                    head.Append($"{name}: {value}\r\n");
                }
                head.Append("\r\n");
                await stream.WriteAsync(Encoding.ASCII.GetBytes(head.ToString()));
            }
            catch (IOException) { }
        }
    }

    private static async Task<ReceivedRequest?> ReadRequestAsync(NetworkStream stream)
    {
        var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int headerEnd = -1;
        while (headerEnd < 0)
        {
            int read = await stream.ReadAsync(chunk);
            if (read == 0) return null;
            buffer.Write(chunk, 0, read);
            headerEnd = IndexOfBlankLine(buffer.GetBuffer(), (int)buffer.Length);
        }
        var bytes = buffer.ToArray();
        var lines = Encoding.ASCII.GetString(bytes, 0, headerEnd).Split("\r\n");
        var start = lines[0].Split(' ');
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines.Skip(1))
        {
            int colon = line.IndexOf(':');
            if (colon > 0) headers[line[..colon].Trim()] = line[(colon + 1)..].Trim();
        }
        int length = headers.TryGetValue("Content-Length", out var value) ? int.Parse(value) : 0;
        var body = new MemoryStream();
        body.Write(bytes, headerEnd + 4, bytes.Length - headerEnd - 4);
        while (body.Length < length)
        {
            int read = await stream.ReadAsync(chunk);
            if (read == 0) break;
            body.Write(chunk, 0, read);
        }
        return new ReceivedRequest(start[0], start[1], headers, Encoding.UTF8.GetString(body.ToArray()));
    }

    private static int IndexOfBlankLine(byte[] data, int length)
    {
        for (int i = 0; i + 3 < length; i++)
        {
            if (data[i] == '\r' && data[i + 1] == '\n' && data[i + 2] == '\r' && data[i + 3] == '\n') return i;
        }
        return -1;
    }
}
