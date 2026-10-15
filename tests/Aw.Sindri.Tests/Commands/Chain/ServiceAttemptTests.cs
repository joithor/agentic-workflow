using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;

namespace Aw.Sindri.Tests.Commands.Chain;

/// <summary>An accepted attempt under a granted service caller (API spec §2.1): it runs, under its own command id, which its audit row carries.</summary>
public sealed class ServiceAttemptTests : IAsyncDisposable
{
    private readonly ChainHarness _c = new(grants: ["jobs"]);

    public ValueTask DisposeAsync() => _c.DisposeAsync();

    [Fact, Trait("Lane", "unit")]
    public async Task Accepted_attempts_under_a_granted_service_answer_with_their_audit_rows_command_ids()
    {
        _ = await _c.H.HoldLease();
        using var service = _c.H.Callers.EnterService("jobs");   // one service context, as a scheduler run holds it
        var run = _c.H.Callers.Current;

        var first = await _c.Run<FixtureCommand, CommandAccepted>(new FixtureCommand("accept"), caller: null);
        var second = await _c.Run<FixtureCommand, CommandAccepted>(new FixtureCommand("accept"), caller: null);

        Assert.True(first.IsSuccess, first.IsError ? first.Error.Message : null);
        Assert.True(second.IsSuccess, second.IsError ? second.Error.Message : null);
        Assert.All(_c.Handled, h => Assert.Equal(CommandOutcome.Accepted, h.Outcome));
        Assert.Equal([first.Value.CommandId, second.Value.CommandId], _c.Handled.Select(h => h.CommandId));
        Assert.Same(run, _c.H.Callers.Current);   // the run's command id is back after each attempt
        Assert.Equal([FixtureService.Done, EventTypes.CommandHandled, FixtureService.Done, EventTypes.CommandHandled], _c.Events.Select(e => e.Envelope.Type),
            StringComparer.Ordinal);
    }
}
