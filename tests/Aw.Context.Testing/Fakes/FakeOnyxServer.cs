using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aw.Context.Testing.Fakes;

/// <summary>
/// A fake Onyx HTTP API on loopback that replays the split-panes recordings (spec §5.2, §10). Pinned to Onyx
/// <see cref="PinnedVersion"/>: 'GET /api/version' (no key, as in Onyx), 'GET /api/document/chunk-info' (a chunk's
/// text), the key check (a service-account key 'on_…' raw or as 'Bearer', a personal access token 'onyx_pat_…' as
/// 'Bearer' only; anything else is Onyx's 403 'Access denied. User is not authenticated.') and the error bodies
/// ('{error_code, detail}', and Onyx's 422 for a bad query parameter) answer as v4.9.0 does. The search route,
/// 'POST /api/query/document-search' (an old Danswer route that v4.9.0 doesn't have), waits on the owner's choice of
/// the real route and is unchanged until then. The Authorization header is never recorded or logged.
/// </summary>
public sealed class FakeOnyxServer : IAsyncDisposable
{
    /// <summary>The credential a server started without options accepts: a service-account key (a fixture value, not a secret).</summary>
    public const string DefaultToken = "on_fixture-onyx";

    /// <summary>The Onyx release the fake answers like, and the version 'GET /api/version' reports by default.</summary>
    public const string PinnedVersion = "v4.9.0";

    private const string ServiceAccountPrefix = "on_";
    private const string PersonalTokenPrefix = "onyx_pat_";
    private const string SearchRoute = "/api/query/document-search";
    private const string ChunkRoute = "/api/document/chunk-info";
    private const string VersionRoute = "/api/version";
    private readonly LoopbackServer _server;

    private FakeOnyxServer(LoopbackServer server, string token)
    {
        _server = server;
        Token = token;
    }

    /// <summary>The base URL, 'http://127.0.0.1:&lt;port&gt;/'.</summary>
    public Uri Url => _server.BaseUrl;

    /// <summary>The credential the server accepts.</summary>
    public string Token { get; }

    /// <summary>The 'Authorization' header value the server accepts for every token kind: 'Bearer ' and <see cref="Token"/>.</summary>
    public string Authorization => "Bearer " + Token;

    /// <summary>The requests received, in order (no headers).</summary>
    public IReadOnlyList<RecordedRequest> Requests => _server.Requests;

    /// <summary>Every log line the server and its web host wrote (kept in memory, never printed).</summary>
    public IReadOnlyList<string> Log => _server.Log;

    /// <summary>Starts the server on an ephemeral loopback port.</summary>
    /// <param name="options">Options; null for the defaults.</param>
    /// <returns>The running server.</returns>
    /// <exception cref="ArgumentException">The token isn't a kind Onyx accepts ('on_…' or 'onyx_pat_…').</exception>
    public static async Task<FakeOnyxServer> Start(FakeServerOptions? options = null)
    {
        var token = options?.Token ?? DefaultToken;
        if (!token.StartsWith(ServiceAccountPrefix, StringComparison.Ordinal) && !token.StartsWith(PersonalTokenPrefix, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Onyx v4.9.0 accepts only a service-account key ('{ServiceAccountPrefix}…') or a personal access token ('{PersonalTokenPrefix}…'); pass a fixture token with one of those prefixes.",
                nameof(options));
        }

        var version = options?.OnyxVersion ?? PinnedVersion;
        var server = await LoopbackServer.Start("fake-onyx", r => Handle(r, token, version)).ConfigureAwait(false);
        return new FakeOnyxServer(server, token);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _server.DisposeAsync();

    private static FakeHttpReply Handle(FakeHttpRequest request, string token, string version)
    {
        try
        {
            return (request.Method, request.Path) switch
            {
                ("GET", VersionRoute) => new FakeHttpReply(200, new JsonObject { ["backend_version"] = version }, "version"),
                _ when !Authenticated(request.Authorization, token) => OnyxError(403, "UNAUTHORIZED", "Access denied. User is not authenticated.", "refused: no valid credential"),
                ("POST", SearchRoute) => Search(request.Body),
                ("GET", ChunkRoute) => Chunk(request.Query),
                _ => OnyxError(404, "NOT_FOUND", $"The fake Onyx server has no route {request.Method} {request.Path}; it answers GET {VersionRoute}, GET {ChunkRoute} and POST {SearchRoute}.", "refused: route"),
            };
        }
        catch (FakeRequestException e)
        {
            return new FakeHttpReply(e.Status, new JsonObject { ["detail"] = e.Detail?.DeepClone() ?? e.Message }, "refused: " + e.Code);
        }
    }

    /// <summary>
    /// Onyx's rule (onyx/auth/utils.py get_hashed_bearer_token_from_request): 'Bearer &lt;token&gt;' for any kind, the raw
    /// value only for a service-account key. An unknown key leaves the caller unauthenticated, the same as no key.
    /// </summary>
    private static bool Authenticated(string? authorization, string token) =>
        string.Equals(authorization, "Bearer " + token, StringComparison.Ordinal)
        || (token.StartsWith(ServiceAccountPrefix, StringComparison.Ordinal) && string.Equals(authorization, token, StringComparison.Ordinal));

    private static FakeHttpReply Search(string body)
    {
        var request = OnyxSearchRequest.Parse(Parse(body));
        var documents = OnyxIndex.Search(request);
        return new FakeHttpReply(200, new JsonObject { ["top_documents"] = documents, ["llm_indices"] = new JsonArray() }, "document-search");
    }

    private static FakeHttpReply Chunk(IReadOnlyDictionary<string, string> query)
    {
        if (query.GetValueOrDefault("document_id") is not { } id
            || !int.TryParse(query.GetValueOrDefault("chunk_id"), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var chunk))
        {
            return ValidationFailed("refused: chunk-info query");
        }

        var document = OnyxIndex.Documents.FirstOrDefault(d => string.Equals(d.Id, id, StringComparison.Ordinal));
        if (document is null || chunk < 0 || chunk >= document.Chunks.Count)
        {
            return OnyxError(404, "NOT_FOUND", "Chunk not found", "chunk-info: not found");
        }

        var text = document.Chunks[chunk];
        var tokens = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        return new FakeHttpReply(200, new JsonObject { ["content"] = text, ["num_tokens"] = tokens }, "chunk-info");
    }

    private static JsonObject Parse(string body)
    {
        try
        {
            return JsonNode.Parse(body) as JsonObject ?? throw OnyxSearchRequest.Unprocessable(["body"], "Must be a JSON object.", "model_type");
        }
        catch (JsonException e)
        {
            throw OnyxSearchRequest.Unprocessable(["body"], "Invalid JSON: " + e.Message, "json_invalid");
        }
    }

    /// <summary>An error as Onyx's handlers write it: '{"error_code", "detail"}' (onyx/error_handling, main.py log_http_error).</summary>
    private static FakeHttpReply OnyxError(int status, string code, string detail, string operation) =>
        new(status, new JsonObject { ["error_code"] = code, ["detail"] = detail }, operation);

    /// <summary>Onyx's 422 for a request that fails FastAPI's validation (main.py validation_exception_handler); it names no field.</summary>
    private static FakeHttpReply ValidationFailed(string operation) =>
        new(422, new JsonObject
        {
            ["status_code"] = 422,
            ["message"] = "Request validation failed.",
            ["data"] = null,
            ["error_code"] = "VALIDATION_ERROR",
            ["detail"] = "Request validation failed.",
        }, operation);
}
