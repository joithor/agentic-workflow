using System.Text.Json;
using Aw.Sindri.Host;
using Aw.Sindri.Tests.ApiGoldens;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>
/// The MCP SDK's own client against a started host's /sindri/mcp, with the repo header a per-repo registration sends.
/// It records the headers of every request it sends, so a test can show what the client puts on the wire.
/// </summary>
internal sealed class McpSession : IAsyncDisposable
{
    private readonly HttpClient _http;

    private McpSession(McpClient client, HttpClient http, RecordingHandler recorder)
    {
        (Client, _http, Recorder) = (client, http, recorder);
    }

    /// <summary>The SDK client.</summary>
    public McpClient Client { get; }

    /// <summary>What each request carried (Origin only; the rest isn't asserted).</summary>
    public IReadOnlyList<SentHeaders> Sent => Recorder.Sent;

    private RecordingHandler Recorder { get; }

    /// <summary>Connects (initialize) as a registration for <paramref name="repo"/> would.</summary>
    /// <param name="host">The started host.</param>
    /// <param name="repo">The X-Sindri-Repo value, or null to send none.</param>
    /// <param name="ct">Cancels the connect.</param>
    /// <param name="extra">More headers on every request.</param>
    /// <returns>The open session.</returns>
    public static async Task<McpSession> Open(GoldenHost host, string? repo, CancellationToken ct, params (string Name, string Value)[] extra)
    {
        var recorder = new RecordingHandler { InnerHandler = new HttpClientHandler() };
        var http = new HttpClient(recorder);
        var headers = extra.ToDictionary(h => h.Name, h => h.Value, StringComparer.Ordinal);
        if (repo is not null)
        {
            headers[SindriHeaders.Repo] = repo;
        }

        var transport = new HttpClientTransport(
            new HttpClientTransportOptions { Endpoint = new Uri(host.Http.BaseAddress!, "/sindri/mcp"), TransportMode = HttpTransportMode.StreamableHttp, AdditionalHeaders = headers },
            http, loggerFactory: null, ownsHttpClient: false);
        var client = await McpClient.CreateAsync(transport, cancellationToken: ct);
        return new McpSession(client, http, recorder);
    }

    /// <summary>Calls <paramref name="tool"/> with <paramref name="arguments"/>.</summary>
    /// <param name="tool">The tool's name.</param>
    /// <param name="arguments">Its arguments, a JSON object.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The tool result.</returns>
    public async Task<CallToolResult> Call(string tool, string arguments, CancellationToken ct)
    {
        using var document = JsonDocument.Parse(arguments);
        var json = document.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone(), StringComparer.Ordinal);
        return await Client.CallToolAsync(new CallToolRequestParams { Name = tool, Arguments = json }, ct);
    }

    public async ValueTask DisposeAsync()
    {
        await Client.DisposeAsync();
        _http.Dispose();
        Recorder.Dispose();
    }

    /// <summary>The headers one request carried.</summary>
    /// <param name="Origin">Its Origin header, or null.</param>
    internal sealed record SentHeaders(string? Origin);

    private sealed class RecordingHandler : DelegatingHandler
    {
        private readonly List<SentHeaders> _sent = [];

        public IReadOnlyList<SentHeaders> Sent
        {
            get
            {
                lock (_sent)
                {
                    return [.. _sent];
                }
            }
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (_sent)
            {
                _sent.Add(new SentHeaders(request.Headers.TryGetValues("Origin", out var origin) ? string.Join(',', origin) : null));
            }

            return base.SendAsync(request, cancellationToken);
        }
    }
}
