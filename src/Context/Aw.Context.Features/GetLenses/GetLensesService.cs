using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Map;
using Aw.Context.Core.Results;
using Aw.Context.Core.Trust;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.GetLenses;

/// <summary>
/// The intent, product and code bands around one node (spec §8.9): the map the caller may read (a session's graph is already cut
/// to its repo), the anchor resolved like any node name, the view built by <see cref="LensesBuilder"/> to the profile's
/// cascade depth, and every label cleaned like every answer's. Only text a model wrote and nobody confirmed is fenced; file names are not.
/// </summary>
internal sealed class GetLensesService(MapReader reader, IScrubberFactory scrubbers) : IService<GetLensesRequest, LensesView>
{
    public async ValueTask<Result<LensesView>> Handle(GetLensesRequest request, CancellationToken cancellationToken)
    {
        var settings = SettingsReader.Read(request.Scope.Settings);
        var built = settings.Bind(s => scrubbers.For(s.ExtraPatterns));
        if (built.IsError)
        {
            return built.Error;
        }

        var read = await reader.ReadView(request.Scope, request.Draft, cancellationToken).ConfigureAwait(false);
        var anchor = read.IsError ? read.Error : AnchorMatch.Resolve(read.Value.Graph, request.Anchor);
        if (read.IsError || anchor.IsError)
        {
            return read.IsError ? read.Error : anchor.Error;
        }

        var view = LensesBuilder.Build(read.Value.Snapshot.Label, request.Draft, read.Value.Graph, anchor.Value.Key, settings.Value.CascadeDepth);
        return view.Map(v => Clean(v, new MapText(built.Value)));
    }

    private static LensesView Clean(LensesView view, MapText text) => view with
    {
        Intent = Band(view.Intent, text, code: false),
        Product = Band(view.Product, text, code: false),
        Code = Band(view.Code, text, code: true),
        Gaps = [.. view.Gaps.Select(text.Line)],
    };

    private static LensBand Band(LensBand band, MapText text, bool code) => band with
    {
        Items = [.. band.Items.Select(i => i with { Label = text.Label(i.Label, code ? Trust.Trusted : TrustPolicy.For(i.Provenance, i.Confirmed)) })],
    };
}
