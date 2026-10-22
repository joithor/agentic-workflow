using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Ports;
using Aw.Kernel.Testing;
using Xunit;

namespace Aw.CodeIndex.Testing;

/// <summary>
/// The <see cref="IEmbedder"/> contract (spec §5.6, §11), run against the fake and against the Ollama adapter over a
/// scripted server (unit lane) and over the real one (heavy lane): the same cases, so a fake cannot drift.
/// </summary>
public abstract class EmbedderContract
{
    /// <summary>Gets the implementation (a heavy-lane class may skip here when no server is up), ready to embed with <see cref="Settings"/>.</summary>
    /// <returns>The embedder.</returns>
    protected abstract Task<IEmbedder> Arrange();

    /// <summary>Gets the settings that reach the implementation's server (a valid loopback URL, the default model).</summary>
    protected abstract EmbeddingsSettings Settings { get; }

    /// <summary>Probing a reachable server with the right model succeeds.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Probe_succeeds_for_the_default_settings()
    {
        var probe = await (await Arrange()).Probe(Settings, TestContext.Current.CancellationToken);

        Check(!probe.IsError, $"a reachable server with the pinned model probes clean ({(probe.IsError ? probe.Error.Code : "ok")})");
    }

    /// <summary>One vector per text, in order, all of one non-zero length.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Embed_returns_one_vector_per_text_in_order_of_one_dimension()
    {
        var embedded = await (await Arrange()).Embed(Settings, ["alpha beta", "gamma delta", "epsilon"], TestContext.Current.CancellationToken);

        Check(!embedded.IsError, "embedding succeeds");
        var vectors = embedded.Value;
        Check(vectors.Count == 3, "one vector per text");
        Check(vectors[0].Length > 0 && vectors.All(v => v.Length == vectors[0].Length), "every vector has the same non-zero length");
        Check(!vectors[0].Span.SequenceEqual(vectors[1].Span), "different texts embed to different vectors");
    }

    /// <summary>The same text embeds to the same vector, in any batch and position.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task The_same_text_embeds_to_the_same_vector()
    {
        var embedder = await Arrange();

        var first = await embedder.Embed(Settings, ["alpha beta", "other"], TestContext.Current.CancellationToken);
        var second = await embedder.Embed(Settings, ["other", "alpha beta"], TestContext.Current.CancellationToken);

        Check(!first.IsError && !second.IsError, "both batches embed");
        Check(Near(first.Value[0].Span, second.Value[1].Span), "a text's vector does not depend on its batch or position");
    }

    /// <summary>An empty batch embeds to nothing and does not fail.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_empty_batch_embeds_to_nothing()
    {
        var embedded = await (await Arrange()).Embed(Settings, [], TestContext.Current.CancellationToken);

        Check(!embedded.IsError && embedded.Value.Count == 0, "no texts, no vectors");
    }

    /// <summary>A URL that is not an IP-literal loopback address is refused with SND-CODEIDX-005 before anything is sent.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_url_that_is_not_loopback_is_refused_by_probe_and_embed()
    {
        var embedder = await Arrange();
        foreach (var url in new[] { "http://localhost:11434", "http://10.0.0.5:11434", "https://127.0.0.1:11434", "http://user:pw@127.0.0.1:11434" })
        {
            var probe = await embedder.Probe(Settings with { Url = url }, TestContext.Current.CancellationToken);
            var embed = await embedder.Embed(Settings with { Url = url }, ["x"], TestContext.Current.CancellationToken);

            Check(probe.IsError && probe.Error.Code == CodeIndexErrors.EmbeddingTargetRefused.Code, $"probe refuses {url}");
            Check(embed.IsError && embed.Error.Code == CodeIndexErrors.EmbeddingTargetRefused.Code, $"embed refuses {url}");
        }
    }

    /// <summary>A model whose name contains 'cloud' is refused with SND-CODEIDX-005.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_cloud_model_is_refused()
    {
        var embedder = await Arrange();

        var probe = await embedder.Probe(Settings with { Model = "gpt-oss:120b-cloud" }, TestContext.Current.CancellationToken);
        var embed = await embedder.Embed(Settings with { Model = "gpt-oss:120b-cloud" }, ["x"], TestContext.Current.CancellationToken);

        Check(probe.IsError && probe.Error.Code == CodeIndexErrors.EmbeddingTargetRefused.Code, "probe refuses a cloud model");
        Check(embed.IsError && embed.Error.Code == CodeIndexErrors.EmbeddingTargetRefused.Code, "embed refuses a cloud model");
    }

    /// <summary>The model name is the settings' model, else a default.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task ModelFor_names_the_settings_model_else_a_default()
    {
        var embedder = await Arrange();

        Check(embedder.ModelFor(Settings with { Model = "some-model:2" }) == "some-model:2", "the settings' model wins");
        Check(!string.IsNullOrWhiteSpace(embedder.ModelFor(Settings with { Model = null })), "no model in the settings still names one");
    }

    private static bool Near(ReadOnlySpan<float> left, ReadOnlySpan<float> right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (var i = 0; i < left.Length; i++)
        {
            if (Math.Abs(left[i] - right[i]) > 1e-4f)
            {
                return false;
            }
        }

        return true;
    }

    private static void Check(bool condition, string clause)
    {
        if (!condition)
        {
            throw new ContractViolationException($"contract: {clause}");
        }
    }
}
