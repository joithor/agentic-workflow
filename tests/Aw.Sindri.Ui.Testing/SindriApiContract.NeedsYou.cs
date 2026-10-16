using Aw.Kernel;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The contract (<see cref="SindriApiContract"/>, continued): the P4 routes answer as the others do, fake and client alike.</summary>
public abstract partial class SindriApiContract
{
    [Fact]
    public async Task NeedsYou_empty_returns_the_served_sentence()
    {
        var server = new FakeSindriApi { NeedsYouView = RouteAnswer.Api("needs-you", "empty") };

        var list = (await Reach(server).NeedsYou(_api, Token)).Value;

        Expect.Sequence([], list.Rows);
        Expect.Equal((string)GoldenFiles.Node("needs-you", "empty")["empty"]!, list.Empty);
    }

    [Fact]
    public async Task NeedsYou_stalled_keeps_rows_and_flags_stalled()
    {
        var server = new FakeSindriApi { NeedsYouView = RouteAnswer.Api("needs-you", "stalled") };

        var list = (await Reach(server).NeedsYou(_api, Token)).Value;

        var served = GoldenFiles.Node("needs-you", "stalled");
        Expect.Equal(served["rows"]!.AsArray().Count, list.Rows.Count);
        Expect.Equal((true, (long)served["asOf"]!["cursor"]!, (long)served["asOf"]!["ledgerHead"]!), (list.AsOf.Stalled, list.AsOf.Cursor, list.AsOf.LedgerHead));
    }

    [Fact]
    public async Task Decide_conflict_is_a_problem_not_an_exception()
    {
        var server = new FakeSindriApi { Decided = RouteAnswer.Problem("decisions-id", "decided-elsewhere") };

        var result = await Reach(server).Decide(_api, "DEC-0192a1b2c3d4e5f6a7b8c9d0e1f2d701", new DecideRequest("accept", Note: null), Token);

        Expect.True(result.IsError);
        Expect.Equal(((string)GoldenFiles.Node("decisions-id", "decided-elsewhere")["title"]!, ErrorKind.Conflict), (result.Error.Code, result.Error.Kind));
    }
}
