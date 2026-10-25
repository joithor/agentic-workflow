using Aw.Judge.Features.ImportLabels;
using Aw.Judge.Testing;

namespace Aw.Judge.Tests.ImportLabels;

// 'judge label import' (cutover spec §5.2): decided decisions in the window become eval items, once each.
// Red until cutover unit 6.3: the stub takes no ports yet, so the lander passes the fakes it builds in.
public sealed class ImportLabelsTests
{
    private static readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task ImportLabels_reports_nothing_imported_for_an_empty_decision_log()
    {
        var result = await new ImportLabelsService().Handle(A.ImportLabelsRequest().Question("ask-check").Build(), _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(new ImportLabelsResponse(Imported: 0, SkippedUnparsable: 0), result.Value);
    }
}
