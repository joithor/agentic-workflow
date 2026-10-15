using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Tests.RunDoctor;

/// <summary>The doctor's 'views' line (API spec §2.3; plan unit E).</summary>
public sealed class DoctorViewsTests
{
    private static readonly DateTimeOffset _at = new(2026, 10, 14, 9, 30, 0, TimeSpan.Zero);

    private static ProjectorState State(long cursor = 4, bool rebuilding = false, long? stalledAt = null, string? type = null) =>
        new(cursor, ViewSchema.Version, _at, rebuilding, stalledAt, type, Fresh: false);

    [Fact, Trait("Lane", "unit")]
    public void Caught_up_or_behind_views_are_ok_with_the_cursor_and_the_head()
    {
        var check = DoctorRules.Views(State(cursor: 4), 6L);

        Assert.Equal(new DoctorCheck("views", CheckState.Ok, "cursor 4 of head 6", Fix: null), check);
    }

    [Fact, Trait("Lane", "unit")]
    public void No_views_store_yet_warns_and_the_fix_starts_sindri()
    {
        var check = DoctorRules.Views((ProjectorState?)null, 0L);

        Assert.Equal(new DoctorCheck("views", CheckState.Warn, "no views yet", "sindri"), check);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_rebuild_warns_with_the_cursor()
    {
        var check = DoctorRules.Views(State(cursor: 3, rebuilding: true), 9L);

        Assert.Equal(new DoctorCheck("views", CheckState.Warn, "rebuilding (cursor 3 of 9)", "wait for the rebuild, then run sindri doctor again"), check);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_stall_fails_with_the_rebuild_fix_and_beats_a_rebuild()
    {
        var check = DoctorRules.Views(State(cursor: 6, rebuilding: true, stalledAt: 7, type: "service.ran"), 9L);

        Assert.Equal(new DoctorCheck("views", CheckState.Fail, "stalled at 7 (service.ran)", "sindri views rebuild"), check);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_read_error_fails_with_its_message()
    {
        var unreadable = DoctorRules.Views(ReadFailingLedger.Failure, 6L);
        var noHead = DoctorRules.Views(State(), ReadFailingLedger.Failure);

        Assert.Equal((CheckState.Fail, ReadFailingLedger.Failure.Message), (unreadable.State, unreadable.Detail));
        Assert.Equal((CheckState.Fail, ReadFailingLedger.Failure.Message), (noHead.State, noHead.Detail));
    }
}
