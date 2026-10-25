using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Commands.Chain;

/// <summary>
/// A non-accepted attempt whose audit row can't be written (API spec §2.1): the caller gets one SND-LEDGER-002 with
/// the attempt's own reason first, no restart advice (nothing is wrong with the process), and the lost row is logged.
/// </summary>
public sealed class LostAuditRowTests : IAsyncDisposable
{
    private static string LedgerCode => SindriErrors.LedgerUnavailable.Code;

    private readonly ChainHarness _c = new();

    private static CallerContext Refused => A.Caller().Service("heartbeat").Build();   // a service the row does not grant: the table still refuses it

    private static CallerContext Human => A.Caller().AtTerminal().Client(ClientLabel.Cli).Build();

    public ValueTask DisposeAsync() => _c.DisposeAsync();

    [Fact, Trait("Lane", "unit")]
    public async Task A_refusal_whose_row_is_lost_names_SND_LEDGER_002_once_without_restart_advice_and_logs_the_lost_row()
    {
        var refused = await _c.Run<FixtureCommand, CommandAccepted>(new FixtureCommand("accept"), Refused);   // no lease held: no grant to write the row with

        Assert.Equal(LedgerCode, refused.Error.Code);
        Assert.DoesNotContain(LedgerCode, refused.Error.Message, StringComparison.Ordinal);   // the code is the error's, not repeated inside it
        Assert.DoesNotContain("restart", refused.Error.Message, StringComparison.Ordinal);
        _ = Assert.Single(_c.Log.Lines, l => l.StartsWith(LedgerCode + " ", StringComparison.Ordinal)
            && l.Contains("FixtureCommand", StringComparison.Ordinal) && l.Contains("refused", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failure_that_is_itself_SND_LEDGER_002_is_not_prefixed_twice()
    {
        await using var c = new ChainHarness(inner => new HandledFailingLedger(inner));
        _ = await c.H.HoldLease();

        var failed = await c.Run<FixtureCommand, CommandAccepted>(new FixtureCommand("full"), Human);

        Assert.Equal(LedgerCode, failed.Error.Code);
        Assert.StartsWith(FixtureService.Full + "; ", failed.Error.Message, StringComparison.Ordinal);   // why: its own reason first, without the code again
        Assert.DoesNotContain(LedgerCode, failed.Error.Message, StringComparison.Ordinal);
        _ = Assert.Single(c.Log.Lines, l => l.StartsWith(LedgerCode + " ", StringComparison.Ordinal) && l.Contains("failed", StringComparison.Ordinal));
    }
}
