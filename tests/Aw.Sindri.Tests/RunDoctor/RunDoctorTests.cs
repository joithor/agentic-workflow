using Aw.Kernel.Testing;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Features.RunDoctor;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.RunDoctor;

public sealed class RunDoctorTests
{
    private readonly SliceHarness _h = new();
    private readonly FakeControlPlaneProbe _api = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private RunDoctorService Service() => new(_h.Store, _h.Probe, _h.Store, _h.Profiles, _h.Profiles, _api, _h.Clock);

    [Fact, Trait("Lane", "unit")]
    public async Task RunDoctor_is_green_when_running_approved_and_answering()
    {
        var approved = SliceHarness.Profile();
        _h.Profiles.Live = approved;
        _ = await _h.Profiles.Save(approved, Token);
        var grant = await _h.HoldLease();
        _api.Answer = (new ControlEndpoint(new Uri("http://127.0.0.1:7410"), grant.Holder.Pid, grant.Epoch), new Ping(grant.Holder.Pid, grant.Epoch));

        var report = (await Service().Handle(new RunDoctorRequest(), Token)).Value;

        Assert.Equal("lease,ledger,profile,api", string.Join(',', report.Checks.Select(c => c.Name)));
        Assert.All(report.Checks, c => Assert.Equal(CheckState.Ok, c.State));
        Assert.Equal(0, report.ExitCode);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task RunDoctor_fails_with_a_fix_when_nothing_runs_and_nothing_is_approved()
    {
        var report = (await Service().Handle(new RunDoctorRequest(), Token)).Value;

        Assert.Equal(2, report.ExitCode);
        var api = report.Checks.Single(c => c.Name == "api");
        Assert.Equal((CheckState.Fail, "sindri"), (api.State, api.Fix));
        Assert.Equal(CheckState.Fail, report.Checks.Single(c => c.Name == "profile").State);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task RunDoctor_fails_the_lease_when_its_holder_died_and_warns_on_a_changed_profile()
    {
        var dead = FakeProcessProbe.Holder(300);
        _h.Probe.Start(dead);
        _ = await _h.Store.Acquire(dead, Token);
        _h.Probe.Kill(300);
        _ = await _h.Profiles.Save(SliceHarness.Profile("5s"), Token);
        _h.Profiles.Live = SliceHarness.Profile("7s");

        var report = (await Service().Handle(new RunDoctorRequest(), Token)).Value;

        Assert.Equal(CheckState.Fail, report.Checks.Single(c => c.Name == "lease").State);
        Assert.Equal((CheckState.Warn, "sindri profile approve"), report.Checks.Single(c => c.Name == "profile") is var p ? (p.State, p.Fix) : default);
        Assert.Equal(KnownInstants.Epoch, _h.Clock.GetUtcNow());
    }
}
