using Aw.BugFix.Core;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;
using Xunit;

namespace Aw.BugFix.Testing;

/// <summary>The <see cref="IStateStore"/> contract, run against the in-memory fake and the JSON file store.</summary>
public abstract class StateStoreContract
{
    private static readonly Ticket _ticket = new(TicketSource.Text, Reference: null, "t", "brief", "expected", "actual");

    /// <summary>Gets the store under test.</summary>
    protected abstract IStateStore Store { get; }

    /// <summary>Gets an empty state directory.</summary>
    protected abstract string Directory { get; }

    /// <summary>No state is AW-BUGFIX-002.</summary>
    [Fact]
    public void No_state_is_AW_BUGFIX_002() =>
        Expect.Equal(BugFixErrors.StateMissing.Code, Store.Load(Directory).Error.Code, "Equal(BugFixErrors.StateMissing.Code, Store.Load(Directory).Error.Code)");

    /// <summary>A saved state loads back with every field.</summary>
    [Fact]
    public void A_saved_state_loads_back()
    {
        var at = new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);
        var state = BugFixState.Start(_ticket, at, "/t.json").Step(Phase.Investigate, "advance investigate", at.AddMinutes(1), "/h.md");

        Store.Save(Directory, state);
        var loaded = Store.Load(Directory).Value;

        Expect.Equal((state.Phase, state.Status, state.Ticket, state.SchemaVersion), (loaded.Phase, loaded.Status, loaded.Ticket, loaded.SchemaVersion), "Equal((state.Phase, state.Status, state.Ticket, state.SchemaVersion), (loaded.Phase, loaded.Status, loaded.Tic");
        Expect.That(state.History.SequenceEqual(loaded.History), "the history round-trips");
        Expect.That(Store.StateFile(Directory).EndsWith("state.json", StringComparison.Ordinal), "EndsWith('state.json', Store.StateFile(Directory), StringComparison.Ordinal)");
    }

    /// <summary>A state file that isn't a BugFix state is AW-BUGFIX-003.</summary>
    [Fact]
    public void An_unreadable_state_is_AW_BUGFIX_003()
    {
        Corrupt();

        Expect.Equal(BugFixErrors.StateCorrupt.Code, Store.Load(Directory).Error.Code, "Equal(BugFixErrors.StateCorrupt.Code, Store.Load(Directory).Error.Code)");
    }

    /// <summary>Makes the state in <see cref="Directory"/> unreadable.</summary>
    protected abstract void Corrupt();
}
