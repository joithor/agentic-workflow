using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Doctor;

namespace Aw.Sindri.Tests.RunDoctor;

/// <summary>The doctor's 'temporal' line (engine spec rev 8 §0 "Operations"): reachable, the pinned version, the namespace present.</summary>
public sealed class TemporalCheckTests
{
    private static readonly TemporalEndpoint _server = new(4321, 17233, 18233);

    [Fact, Trait("Lane", "unit")]
    public void A_ready_server_at_the_pin_with_the_namespace_is_ok()
    {
        var check = DoctorRules.Temporal(new TemporalFacts(_server, "1.9.1", "1.9.1", Reachable: true, NamespacePresent: true, Problem: null));

        Assert.Equal(new DoctorCheck("temporal", CheckState.Ok, "127.0.0.1:17233 answers (pid 4321), version 1.9.1 as pinned, namespace sindri present", Fix: null), check);
    }

    [Fact, Trait("Lane", "unit")]
    public void No_server_on_the_home_database_fails_with_the_fix_to_start_sindri()
    {
        var check = DoctorRules.Temporal(new TemporalFacts(Running: null, "1.9.1", Version: null, Reachable: false, NamespacePresent: false, Problem: null));

        Assert.Equal(new DoctorCheck("temporal", CheckState.Fail, "not running: no dev server on this home's temporal.db", "sindri"), check);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_server_that_doesnt_answer_fails_with_the_reason()
    {
        var check = DoctorRules.Temporal(new TemporalFacts(_server, "1.9.1", "1.9.1", Reachable: false, NamespacePresent: false, Problem: "connection refused"));

        Assert.Equal(new DoctorCheck("temporal", CheckState.Fail, "pid 4321 on 127.0.0.1:17233 doesn't answer: connection refused", "sindri stop, then sindri"), check);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_version_other_than_the_pin_fails_with_the_fetch()
    {
        var check = DoctorRules.Temporal(new TemporalFacts(_server, "1.9.1", "1.8.0", Reachable: true, NamespacePresent: true, Problem: null));

        Assert.Equal(new DoctorCheck("temporal", CheckState.Fail, "pid 4321 on 127.0.0.1:17233 is version 1.8.0, but binaries.lock pins 1.9.1",
            "scripts/binaries-lock.sh fetch temporal, then sindri stop and sindri"), check);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_server_without_the_namespace_fails()
    {
        var check = DoctorRules.Temporal(new TemporalFacts(_server, "1.9.1", "1.9.1", Reachable: true, NamespacePresent: false, Problem: "namespace not found"));

        Assert.Equal(new DoctorCheck("temporal", CheckState.Fail, "pid 4321 on 127.0.0.1:17233 answers, but has no namespace sindri", "sindri stop, then sindri"), check);
    }
}
