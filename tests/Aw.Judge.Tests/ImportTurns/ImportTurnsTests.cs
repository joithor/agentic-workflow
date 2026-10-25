using Aw.Judge.Features.ImportTurns;
using Aw.Judge.Testing;

namespace Aw.Judge.Tests.ImportTurns;

// 'judge eval import-turns <transcript.jsonl>...': per-turn eval items from the edits in a transcript.
// Red until cutover unit 6.3: the stub takes no ports yet, so the lander passes the fakes it builds in.
public sealed class ImportTurnsTests
{
    private static readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task ImportTurns_counts_a_file_it_cannot_read_as_unreadable_and_imports_nothing()
    {
        var result = await new ImportTurnsService().Handle(A.ImportTurnsRequest().Files(Path.Combine(Path.GetTempPath(), "no-such-transcript.jsonl")).Build(), _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(new ImportTurnsResponse(Imported: 0, Skipped: 0, Unreadable: 1), result.Value);
    }
}
