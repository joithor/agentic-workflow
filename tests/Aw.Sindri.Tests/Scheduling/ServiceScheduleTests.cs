using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Tests.Scheduling;

public sealed class ServiceScheduleTests
{
    private static readonly DateTimeOffset _t0 = KnownInstants.Epoch;
    private static readonly ServiceSchedule _every5s = new(ServiceName.From("heartbeat"), new Cadence(TimeSpan.FromSeconds(5)), _t0);

    [Fact, Trait("Lane", "unit")]
    public void On_time_the_next_slot_is_one_cadence_later() =>
        Assert.Equal(_t0.AddSeconds(5), _every5s.Advance(_t0.AddMilliseconds(10)).NextSlot);

    [Fact, Trait("Lane", "unit")]
    public void After_a_sleep_missed_slots_are_skipped_not_replayed() =>
        Assert.Equal(_t0.AddSeconds(65), _every5s.Advance(_t0.AddSeconds(62)).NextSlot);

    [Fact, Trait("Lane", "unit")]
    public void The_profile_names_the_services_and_their_cadence()
    {
        var profile = new SindriProfile(1, "thor",
            Services: new Dictionary<string, ServiceSettings>(StringComparer.Ordinal) { ["heartbeat"] = new("2m") });

        var only = Assert.Single(ServiceSchedule.From(profile, _t0));

        Assert.Equal(new ServiceSchedule(ServiceName.From("heartbeat"), new Cadence(TimeSpan.FromMinutes(2)), _t0), only);
    }
    [Fact, Trait("Lane", "unit")]
    public void A_built_in_service_runs_on_its_own_cadence_without_a_profile_entry()
    {
        var bridge = new BuiltInService(ServiceName.From("workflow-events"), new Cadence(TimeSpan.FromSeconds(5)));
        var profile = new SindriProfile(1, "thor",
            Services: new Dictionary<string, ServiceSettings>(StringComparer.Ordinal) { ["heartbeat"] = new("2m") });

        var withProfile = ServiceSchedule.From(profile, [bridge], _t0);
        var withoutProfile = ServiceSchedule.From(profile: null, [bridge], _t0);

        Assert.Equal(
            [new ServiceSchedule(ServiceName.From("heartbeat"), new Cadence(TimeSpan.FromMinutes(2)), _t0), new ServiceSchedule(bridge.Name, bridge.Cadence, _t0)],
            withProfile);
        Assert.Equal([new ServiceSchedule(bridge.Name, bridge.Cadence, _t0)], withoutProfile);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_profile_entry_sets_a_built_in_service_s_cadence()
    {
        var bridge = new BuiltInService(ServiceName.From("workflow-events"), new Cadence(TimeSpan.FromSeconds(5)));
        var profile = new SindriProfile(1, "thor",
            Services: new Dictionary<string, ServiceSettings>(StringComparer.Ordinal) { ["workflow-events"] = new("30s") });

        var only = Assert.Single(ServiceSchedule.From(profile, [bridge], _t0));

        Assert.Equal(new ServiceSchedule(bridge.Name, new Cadence(TimeSpan.FromSeconds(30)), _t0), only);
    }

    private static readonly Cadence _hourly = new(TimeSpan.FromHours(1));

    [Fact, Trait("Lane", "unit")]
    public void A_service_with_no_recorded_run_starts_now() =>
        Assert.Equal(_t0, ServiceSchedule.FirstSlot(lastRun: null, _hourly, _t0));

    [Fact, Trait("Lane", "unit")]
    public void A_service_that_ran_within_its_period_continues_its_series() =>
        Assert.Equal(_t0.AddMinutes(40), ServiceSchedule.FirstSlot(_t0.AddMinutes(-20), _hourly, _t0));

    [Fact, Trait("Lane", "unit")]
    public void A_service_overdue_starts_now_once()
    {
        Assert.Equal(_t0, ServiceSchedule.FirstSlot(_t0.AddHours(-30), _hourly, _t0));
        Assert.Equal(_t0, ServiceSchedule.FirstSlot(_t0.AddHours(-1), _hourly, _t0));   // due exactly now
    }

    [Fact, Trait("Lane", "unit")]
    public void A_last_run_in_the_future_never_pushes_the_series_past_one_period_from_now() =>
        Assert.Equal(_t0.AddHours(1), ServiceSchedule.FirstSlot(_t0.AddDays(3), _hourly, _t0));

    [Fact, Trait("Lane", "unit")]
    public void A_cadence_change_re_anchors_the_last_run_on_the_new_interval()
    {
        var last = _t0.AddMinutes(-20);

        Assert.Equal(_t0.AddMinutes(40), ServiceSchedule.FirstSlot(last, _hourly, _t0));
        Assert.Equal(_t0.AddMinutes(100), ServiceSchedule.FirstSlot(last, new Cadence(TimeSpan.FromHours(2)), _t0));
        Assert.Equal(_t0, ServiceSchedule.FirstSlot(last, new Cadence(TimeSpan.FromMinutes(10)), _t0));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_schedule_anchors_each_profile_and_built_in_service_on_its_own_last_run()
    {
        var bridge = new BuiltInService(ServiceName.From("workflow-events"), new Cadence(TimeSpan.FromMinutes(10)));
        var fresh = new BuiltInService(ServiceName.From("fresh"), new Cadence(TimeSpan.FromMinutes(10)));
        var profile = new SindriProfile(1, "thor",
            Services: new Dictionary<string, ServiceSettings>(StringComparer.Ordinal) { ["heartbeat"] = new("1h"), ["overdue"] = new("1h"), ["new"] = new("1h") });
        var heartbeat = ServiceName.From("heartbeat");
        var lastRuns = new Dictionary<ServiceName, DateTimeOffset>
        {
            [heartbeat] = _t0.AddMinutes(-20),
            [ServiceName.From("overdue")] = _t0.AddHours(-30),
            [bridge.Name] = _t0.AddMinutes(-4),   // a manual run's slot anchors like a scheduled one
        };

        var slots = ServiceSchedule.From(profile, [bridge, fresh], _t0, lastRuns).ToDictionary(s => s.Name.Value, s => s.NextSlot, StringComparer.Ordinal);

        Assert.Equal(
            new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal)
            {
                ["heartbeat"] = _t0.AddMinutes(40),
                ["overdue"] = _t0,
                ["new"] = _t0,
                ["workflow-events"] = _t0.AddMinutes(6),
                ["fresh"] = _t0,
            },
            slots);
    }
}
