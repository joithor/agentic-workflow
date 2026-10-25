using System.Net;
using System.Text;
using System.Text.Json;
using Aw.CodeIndex.Testing;

namespace Aw.CodeIndex.Tests.Embedding;

/// <summary>A scripted Ollama behind an <see cref="HttpMessageHandler"/>: /api/tags lists <see cref="Models"/> and /api/embed answers <see cref="FakeEmbedder.VectorOf"/> of each input. No socket is opened.</summary>
internal sealed class FakeOllamaServer : HttpMessageHandler
{
    public const string Model = "nomic-embed-text:latest";
    public const string Digest = "0a109f422b47e3a30ba2b10eca18548e944e8a23073ee3f3e947efcf3c45e59f";

    public Dictionary<string, string> Models { get; } = new(StringComparer.Ordinal) { [Model] = Digest };

    public List<(HttpMethod Method, Uri Uri, string Body)> Requests { get; } = [];

    /// <summary>When set, every request answers with this status (a redirect carries a Location).</summary>
    public HttpStatusCode? Status { get; set; }

    /// <summary>When true, every request fails as a refused connection does.</summary>
    public bool Down { get; set; }

    /// <summary>When true, /api/embed leaves the last vector out.</summary>
    public bool DropLastVector { get; set; }

    /// <summary>When set, /api/embed answers this body instead of vectors.</summary>
    public string? EmbedBody { get; set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add((request.Method, request.RequestUri!, body));
        if (Down)
        {
            throw new HttpRequestException("connection refused");
        }

        if (Status is { } status)
        {
            var scripted = new HttpResponseMessage(status);
            scripted.Headers.Location = new Uri("http://example.invalid/api/tags");
            return scripted;
        }

        return request.RequestUri!.AbsolutePath switch
        {
            "/api/tags" => Json(JsonSerializer.Serialize(new { models = Models.Select(m => new { name = m.Key, model = m.Key, digest = m.Value }) })),
            "/api/embed" => Embed(body),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        };
    }

    private static HttpResponseMessage Json(string text) => new(HttpStatusCode.OK) { Content = new StringContent(text, Encoding.UTF8, "application/json") };

    private HttpResponseMessage Embed(string body)
    {
        using var document = JsonDocument.Parse(body);
        var model = document.RootElement.GetProperty("model").GetString()!;
        if (!Models.ContainsKey(model))
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("""{"error":"model not found"}""") };
        }

        if (EmbedBody is { } scripted)
        {
            return Json(scripted);
        }

        var vectors = document.RootElement.GetProperty("input").EnumerateArray().Select(i => FakeEmbedder.VectorOf(i.GetString()!)).ToList();
        if (DropLastVector && vectors.Count > 0)
        {
            vectors.RemoveAt(vectors.Count - 1);
        }

        return Json(JsonSerializer.Serialize(new { model, embeddings = vectors }));
    }
}
