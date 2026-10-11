using Aw.Evidence.Core;
using Aw.Evidence.Core.Browser;
using Aw.Evidence.Core.Files;
using Aw.Evidence.Core.Images;
using Aw.Evidence.Core.Runs;
using Aw.Kernel;

namespace Aw.Evidence.Features.Parity;

/// <summary>
/// Screenshots the viewport, crops the design export and the implementation to the same size, compares them pixel by
/// pixel and writes the design, implementation, diff and side-by-side images.
/// </summary>
internal sealed class FrameComparison(IRunFiles files, IImageCodec codec)
{
    internal async ValueTask<Result<ParityMeasurement>> CompareAsync(IBrowserSession session, ParityContext context, FrameRegion target, CancellationToken cancellationToken)
    {
        var frame = target.Frame;
        var shot = await session.ScreenshotAsync(cancellationToken).ConfigureAwait(false);
        var design = await files.ReadAsync(context.DesignPath(frame), cancellationToken).ConfigureAwait(false);
        var implementation = shot.Bind(b => codec.Decode(b)).Bind(image => ImageOps.Crop(image, target.Region));
        var expected = design.Bind(b => codec.Decode(b)).Bind(image => ImageOps.Crop(image, frame.DesignRegion));
        var compared = expected.Bind(e => implementation.Bind(i => PixelMatch.Compare(e, i, new PixelMatchOptions(context.Manifest.Threshold, context.Manifest.IncludeAA))));
        if (compared.IsError)
        {
            return EvidenceErrors.RunFailed.With($"frame {frame.Name}: {compared.Error.Message}");
        }

        var paths = ParityFiles.For(context.RunDirectory, frame.Name);
        var written = await WriteAsync(paths, expected.Value, implementation.Value, compared.Value.Diff, cancellationToken).ConfigureAwait(false);
        return written.IsError
            ? written.Error
            : new ParityMeasurement(frame, target.Anchor, compared.Value, BoxChecks: [], paths);
    }

    private async ValueTask<Result<Unit>> WriteAsync(ParityFiles paths, RgbaImage design, RgbaImage implementation, RgbaImage diff, CancellationToken cancellationToken)
    {
        (string Path, RgbaImage Image)[] images =
        [
            (paths.Design, design), (paths.Implementation, implementation), (paths.Diff, diff),
            (paths.SideBySide, ImageOps.SideBySide(design, implementation, diff)),
        ];
        foreach (var (path, image) in images)
        {
            var written = await files.WriteAsync(path, codec.Encode(image), cancellationToken).ConfigureAwait(false);
            if (written.IsError)
            {
                return written.Error;
            }
        }

        return Unit.Value;
    }
}
