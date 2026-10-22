using System.Globalization;
using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Embedding;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Ports;
using Aw.Kernel;

namespace Aw.CodeIndex.Features.BuildCodeIndex;

/// <summary>The embeddings layer of a build (spec §5.6, §5.7): probes the model, embeds the symbols with stored text in batches and records the layer's row.</summary>
/// <param name="embedder">The embedding model.</param>
/// <param name="clock">The time source.</param>
public sealed class EmbeddingsLayer(IEmbedder embedder, TimeProvider clock)
{
    /// <summary>Runs the layer and records its row; a model that fails leaves the layer unavailable with the failure's code, never a failed build.</summary>
    /// <param name="build">The build file.</param>
    /// <param name="settings">The embeddings settings.</param>
    /// <param name="progress">Told a counts-only line per batch.</param>
    /// <param name="cancellationToken">Cancels the layer between batches.</param>
    /// <returns>How many symbols have a vector and how many should; an error only when the build file cannot be read or written.</returns>
    public async ValueTask<Result<EmbeddingProgress>> Run(IIndexBuild build, EmbeddingsSettings settings, IProgress<string> progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(progress);
        var model = embedder.ModelFor(settings);
        if (!settings.Enabled)
        {
            var off = await Record(build, new LayerRecord(LayerName.Embeddings, "none", LayerState.Disabled, Code: null, "disabled by the profile", clock.GetUtcNow()), cancellationToken).ConfigureAwait(false);
            return off.IsError ? off.Error : new EmbeddingProgress(0, 0);
        }

        var probed = await embedder.Probe(settings, cancellationToken).ConfigureAwait(false);
        return probed.IsError
            ? await Settle(build, model, probed.Error, cancellationToken).ConfigureAwait(false)
            : await Embed(build, settings, model, progress, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<EmbeddingProgress>> Embed(IIndexBuild build, EmbeddingsSettings settings, string model, IProgress<string> progress, CancellationToken cancellationToken)
    {
        var before = await build.CountEmbeddings(cancellationToken).ConfigureAwait(false);
        var candidates = await build.EmbedCandidates(CodeIndexRules.MaxEmbeddedPerBuild, cancellationToken).ConfigureAwait(false);
        if (before.IsError || candidates.IsError)
        {
            return before.IsError ? before.Error : candidates.Error;
        }

        var done = before.Value.Embedded;
        foreach (var batch in candidates.Value.Chunk(CodeIndexRules.EmbedBatchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var vectors = await embedder.Embed(settings, [.. batch.Select(EmbedText.Of)], cancellationToken).ConfigureAwait(false);
            if (vectors.IsError)
            {
                return await Settle(build, model, vectors.Error, cancellationToken).ConfigureAwait(false);
            }

            var written = await build.WriteEmbeddings([.. batch.Select((c, i) => new EmbeddingRecord(c.Symbol, model, vectors.Value[i]))], cancellationToken).ConfigureAwait(false);
            if (written.IsError)
            {
                return written.Error;
            }

            done += batch.Length;
            progress.Report(string.Create(CultureInfo.InvariantCulture, $"embeddings {done:N0} of {before.Value.Embeddable:N0}"));
        }

        return await Settle(build, model, failure: null, cancellationToken).ConfigureAwait(false);
    }

    // Records what the layer ended as: unavailable with the failure's code when the model failed (the vectors made so far stay),
    // otherwise ok when every symbol has a vector and pending when the cap left some for the next build.
    private async ValueTask<Result<EmbeddingProgress>> Settle(IIndexBuild build, string model, Error? failure, CancellationToken cancellationToken)
    {
        var counted = await build.CountEmbeddings(cancellationToken).ConfigureAwait(false);
        if (counted.IsError)
        {
            return counted.Error;
        }

        var (embedded, embeddable) = (counted.Value.Embedded, counted.Value.Embeddable);
        var state = failure is not null ? LayerState.Unavailable : embedded >= embeddable ? LayerState.Ok : LayerState.Pending;
        var detail = string.Create(CultureInfo.InvariantCulture, $"{embedded} of {embeddable} embedded");
        var recorded = await Record(build, new LayerRecord(LayerName.Embeddings, model, state, failure?.Code, detail, clock.GetUtcNow()), cancellationToken).ConfigureAwait(false);
        return recorded.IsError ? recorded.Error : counted.Value;
    }

    private static async ValueTask<Result<Unit>> Record(IIndexBuild build, LayerRecord layer, CancellationToken cancellationToken)
        => await build.SetLayer(layer, cancellationToken).ConfigureAwait(false);
}
