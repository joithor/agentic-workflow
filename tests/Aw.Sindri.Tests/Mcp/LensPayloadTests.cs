using System.Text;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Results;
using Aw.Kernel.Json;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Mcp;

namespace Aw.Sindri.Tests.Mcp;

public sealed class LensPayloadTests
{
    private static readonly System.Text.Json.JsonSerializerOptions _options = AwJson.CreateOptions(SindriJsonContext.Default);

    private static LensesView Lenses(int files)
    {
        var node = NodeKey.From("feature:tabs.tab-manager");
        var link = LinkId.From("edge:0123456789abcdef");
        var list = Enumerable.Range(0, files)
            .Select(i => new LensFile($"src/tabs/{new string('x', 200)}{i}.ts", [node], [], Provenance.Model, 0.74, Confirmed: false, Suggested: true, link, $"src/tabs/{new string('x', 200)}{i}.ts", NodeKind.Code, Trust.Untrusted))
            .ToList();
        return new LensesView(1, new LensAnchor(node, "Tab manager", NodeKind.Feature, Trust.Trusted), "v13", Draft: false, 2, DateTimeOffset.UnixEpoch,
            new LensBands(
                new IntentBand([], [], new IntentCounts(0, 0, Orphaned: null)),
                new ProductBand([], [], [], new ProductCounts(0, 0), []),
                new CodeBand(list, LensSymbols.Unavailable, new CodeCounts(files, 0, files))),
            [], [],
            new LensCoverage(6, new CoverageIntent(4, 6), new CoverageProduct(3, 6), new CoverageCode(2, 6, "file", 1), []),
            new LensSearched([], "v13", 2, [], [], new SearchedBands(new BandSearched(Empty: false), new BandSearched(Empty: false), new BandSearched(Empty: false))),
            [], new Dictionary<string, Citation>(StringComparer.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_view_that_fits_is_returned_as_it_is()
    {
        var view = Lenses(files: 3);

        var (fitted, truncated) = LensPayload.Fit(view, _options);

        Assert.False(truncated);
        Assert.Same(view, fitted);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_view_over_the_budget_is_cut_down_until_its_json_fits()
    {
        var view = Lenses(files: 400);

        var (fitted, truncated) = LensPayload.Fit(view, _options);

        Assert.True(truncated);
        Assert.InRange(Encoding.UTF8.GetCharCount(AwJson.Serialize(fitted, _options)), 1, LensPayload.Budget);
        Assert.NotEmpty(fitted.Bands.Code.Files);
        Assert.True(fitted.Bands.Code.Files.Count < 400);
    }
}
