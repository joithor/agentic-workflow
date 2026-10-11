using System.Text.Json;
using Aw.Evidence.Core.Files;
using Aw.Evidence.Core.Geometry;
using Aw.Evidence.Core.Images;
using Aw.Evidence.Core.Judging;
using Aw.Evidence.Core.Usage;
using Aw.Kernel;

namespace Aw.Evidence.Core.Visual;

/// <summary>
/// Decides whether a run needs a model at all, cheapest first: a deterministic pixel compare with the approved baseline
/// (unchanged pixels never reach a model), then the verdict cache, then one critique of the changed region. Fails
/// closed: an unreadable image skips that stage, and no critique leaves the verdict unchecked. A run with failed steps
/// is never 'unchanged'.
/// </summary>
/// <param name="files">Reads screenshots, writes the diff and crops.</param>
/// <param name="codec">Decodes and encodes PNGs.</param>
/// <param name="judge">The critique.</param>
/// <param name="clock">Times the critique.</param>
/// <param name="json">The package's AwJson options (cache keys).</param>
public sealed class VisualGate(IRunFiles files, IImageCodec codec, IJudge judge, TimeProvider clock, JsonSerializerOptions json)
{
    /// <summary>Decides the run's visual verdict.</summary>
    /// <param name="input">The screenshots, run and cache.</param>
    /// <param name="log">Where the critique (or cache hit) is recorded.</param>
    /// <param name="cancellationToken">Cancels the decision.</param>
    /// <returns>The outcome, with the cache to save.</returns>
    public async ValueTask<VisualOutcome> DecideAsync(VisualGateInput input, InvocationLog log, CancellationToken cancellationToken)
    {
        var after = await files.ReadAsync(input.AfterScreenshot, cancellationToken).ConfigureAwait(false);
        var baseline = input.Baseline is null ? null : await files.ReadAsync(input.Baseline, cancellationToken).ConfigureAwait(false);
        var diff = after.IsSuccess && baseline is { IsSuccess: true }
            ? await CompareAsync(after.Value, baseline.Value, input.RunDirectory, cancellationToken).ConfigureAwait(false)
            : null;
        if (diff is { IsChanged: false } && !input.HasFailedSteps)
        {
            return new(Verdict.Unchanged, [], diff.Score, diff.DiffImage, input.Cache);
        }

        var key = KeyFor(after, baseline, input.Context);
        if (key is not null && input.Cache.Lookup(key, input.Context.AppBuild is not null, input.RunId) is { } cached)
        {
            log.Record(new(InvocationPhase.VisualCritique, Model: null, ElapsedMs: 0, Ok: true, InputTokens: null, OutputTokens: null, CacheHit: true));
            return new(ToVerdict(cached.Decision), cached.Reasons, diff?.Score, diff?.DiffImage, input.Cache);
        }

        return await CritiqueAsync(input, diff, key, log, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The run verdict for a judge decision.</summary>
    /// <param name="decision">The decision.</param>
    /// <returns>The verdict.</returns>
    public static Verdict ToVerdict(VisualDecision decision) => decision switch
    {
        VisualDecision.LooksRight => Verdict.LooksRight,
        VisualDecision.LooksOff => Verdict.LooksOff,
        VisualDecision.Sloppy => Verdict.Sloppy,
        _ => Verdict.Unchecked,
    };

    private async ValueTask<VisualOutcome> CritiqueAsync(
        VisualGateInput input, BaselineDiff? diff, string? key, InvocationLog log, CancellationToken cancellationToken)
    {
        var request = await CritiqueRequestAsync(input, diff, cancellationToken).ConfigureAwait(false);
        var verdict = await log.TimeAsync(InvocationPhase.VisualCritique, clock, () => judge.CritiqueAsync(request, cancellationToken), static v => v.Usage).ConfigureAwait(false);
        if (verdict is null)
        {
            return new(Verdict.Unchecked, [], diff?.Score, diff?.DiffImage, input.Cache);
        }

        var cache = key is null ? input.Cache : input.Cache.With(key, verdict, input.RunId);
        return new(ToVerdict(verdict.Decision), verdict.Reasons, diff?.Score, diff?.DiffImage, cache);
    }

    // An unreadable image has no key: the cache is skipped and the critique decides.
    private string? KeyFor(Result<byte[]> after, Result<byte[]>? baseline, VerdictCacheContext context) =>
        after.IsError || baseline is { IsError: true }
            ? null
            : new VerdictCacheKey(ContentHash.Sha256Hex(after.Value), baseline is null ? null : ContentHash.Sha256Hex(baseline.Value), context).Compute(json);

    private async ValueTask<BaselineDiff?> CompareAsync(byte[] after, byte[] baseline, string runDirectory, CancellationToken cancellationToken)
    {
        var a = codec.Decode(after);
        var b = codec.Decode(baseline);
        if (a.IsError || b.IsError)
        {
            return null;   // unreadable: never assume unchanged; the critique decides
        }

        var compared = PixelMatch.Compare(b.Value, a.Value, PixelMatchOptions.Default);
        if (compared.IsError)
        {
            return new BaselineDiff(1, Changed: null, DiffImage: null);   // a size change is a change
        }

        var path = RunPath.Combine(runDirectory, "visual-diff.png");
        var written = await files.WriteAsync(path, codec.Encode(compared.Value.Diff), cancellationToken).ConfigureAwait(false);
        return new BaselineDiff(compared.Value.DiffScore, compared.Value.Changed, written.IsSuccess ? path : null);
    }

    private async ValueTask<VisualCritiqueRequest> CritiqueRequestAsync(VisualGateInput input, BaselineDiff? diff, CancellationToken cancellationToken)
    {
        if (diff?.Changed is { } box && input.Baseline is { } baseline)
        {
            var afterCrop = await CropAsync(input.AfterScreenshot, box, RunPath.Combine(input.RunDirectory, "crop-after.png"), cancellationToken).ConfigureAwait(false);
            var baselineCrop = await CropAsync(baseline, box, RunPath.Combine(input.RunDirectory, "crop-baseline.png"), cancellationToken).ConfigureAwait(false);
            if (afterCrop is not null && baselineCrop is not null)
            {
                return new(afterCrop, baselineCrop, input.RunDirectory);
            }
        }

        return new(input.AfterScreenshot, input.Baseline, input.RunDirectory);   // no region (or the crop failed): the full images
    }

    private async ValueTask<string?> CropAsync(string source, Region box, string target, CancellationToken cancellationToken)
    {
        var bytes = await files.ReadAsync(source, cancellationToken).ConfigureAwait(false);
        var crop = bytes.Bind(b => codec.Decode(b)).Bind(image => ImageOps.CropPadded(image, box));
        if (crop.IsError)
        {
            return null;
        }

        var written = await files.WriteAsync(target, codec.Encode(crop.Value), cancellationToken).ConfigureAwait(false);
        return written.IsSuccess ? target : null;
    }
}
