using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.ListSessions;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>The shared lookup and the human-only refusal (collector spec CA-2, CA-9).</summary>
public sealed class SessionRowsTests
{
    private static readonly SessionSnapshot _pass = new(SessionsRig.Three, [], [], SessionData.T0);

    [Fact, Trait("Lane", "unit")]
    public void Find_answers_the_session_to_a_human_and_to_a_session_of_its_repo_and_null_to_another_repo()
    {
        var reference = SessionsRig.Claude().Ref;

        Assert.NotNull(SessionRows.Find(_pass, reference, A.Caller().Human().Build()));
        Assert.NotNull(SessionRows.Find(_pass, reference, A.Caller().Session().InRepo("api").Build()));
        Assert.Null(SessionRows.Find(_pass, reference, A.Caller().Session().InRepo("web").Build()));
        Assert.Null(SessionRows.Find(_pass, reference, A.Caller().Session().Build()));
    }

    [Fact, Trait("Lane", "unit")]
    public void Unavailable_reads_the_typed_reason_not_the_sentence()
    {
        var reworded = new SessionProblem("processes", "SND-SESSION-002", "any wording") { Reason = UnavailableReason.ProcUnreadable };
        var sentenceOnly = new SessionProblem("processes", "SND-SESSION-002", "Sindri can't see agent sessions on this system: disabled");

        Assert.Equal("proc-unreadable", SessionRows.Unavailable([reworded])!.Message);
        Assert.Null(SessionRows.Unavailable([sentenceOnly]));
    }
}
