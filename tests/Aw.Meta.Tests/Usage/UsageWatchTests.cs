using Aw.Meta.Core.Config;
using Aw.Meta.Core.Usage;

namespace Aw.Meta.Tests.Usage;

public sealed class UsageWatchTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 18, 20, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _reset = new(2026, 10, 19, 9, 50, 0, TimeSpan.Zero);
    private static readonly UsageConfig _config = new();

    private static UsageStop Session(DateTimeOffset? resets) => new(UsageKind.Session, resets, "You've hit your session limit");

    private static UsageReading Limited(string subject, UsageStop stop, bool idle = true) => new(subject, UsageState.Limited, stop, idle, Source: "pane");

    private static UsageReading Ok(string subject) => new(subject, UsageState.Ok, Stop: null, Idle: false);

    private static UsageStep Step(UsageMemory memory, DateTimeOffset at, params UsageReading[] readings) =>
        UsageWatch.Step(memory, readings, at, _config);

    private static UsageStep StepWith(UsageConfig config, UsageMemory memory, DateTimeOffset at, params UsageReading[] readings) =>
        UsageWatch.Step(memory, readings, at, config);

    [Fact, Trait("Lane", "unit")]
    public void A_new_stop_is_announced_once_and_logged_and_then_nothing_while_it_lasts()
    {
        var first = Step(UsageMemory.Empty, _now, Limited("api", Session(_reset)));
        var later = Step(first.Memory, _now.AddHours(1), Limited("api", Session(_reset)));

        Assert.Equal(["USAGE LIMITED api session resets 2026-10-19 09:50:00Z"], first.Lines);
        Assert.Contains("api", Assert.Single(first.Notes), StringComparison.Ordinal);
        Assert.Empty(first.Resume);
        Assert.Empty(later.Lines);
        Assert.Empty(later.Notes);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_re_armed_watch_reads_the_memory_back_and_does_not_announce_the_stop_again()
    {
        var first = Step(UsageMemory.Empty, _now, Limited("api", Session(_reset)));
        var rearmed = UsageMemory.Parse(first.Memory.Format());

        var step = Step(rearmed, _now.AddMinutes(30), Limited("api", Session(_reset)));

        Assert.Empty(step.Lines);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_window_is_back_at_the_reset_time_plus_the_margin_and_not_a_second_before()
    {
        var memory = Step(UsageMemory.Empty, _now, Limited("api", Session(_reset))).Memory;

        var early = Step(memory, _reset.AddSeconds(119), Limited("api", Session(_reset)));
        var due = Step(memory, _reset.AddSeconds(120), Limited("api", Session(_reset)));

        Assert.Empty(early.Lines);
        Assert.Empty(early.Resume);
        Assert.Equal(["USAGE OK api"], due.Lines);
        Assert.Equal(["api"], due.Resume);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_margin_comes_from_the_config()
    {
        var memory = Step(UsageMemory.Empty, _now, Limited("api", Session(_reset))).Memory;

        var step = StepWith(new UsageConfig(ResumeMarginSeconds: 30), memory, _reset.AddSeconds(30), Limited("api", Session(_reset)));

        Assert.Equal(["USAGE OK api"], step.Lines);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_busy_lane_is_announced_but_never_typed_into_and_is_resumed_once_it_is_idle()
    {
        var memory = Step(UsageMemory.Empty, _now, Limited("api", Session(_reset))).Memory;
        var busy = Step(memory, _reset.AddMinutes(5), Limited("api", Session(_reset), idle: false));
        var idle = Step(busy.Memory, _reset.AddMinutes(6), Limited("api", Session(_reset), idle: true));
        var after = Step(idle.Memory, _reset.AddMinutes(7), Limited("api", Session(_reset), idle: true));

        Assert.Equal(["USAGE OK api"], busy.Lines);
        Assert.Empty(busy.Resume);
        Assert.Empty(idle.Lines);
        Assert.Equal(["api"], idle.Resume);
        Assert.Empty(after.Resume);
    }

    [Fact, Trait("Lane", "unit")]
    public void With_auto_resume_off_the_window_is_announced_and_nothing_is_typed()
    {
        var off = new UsageConfig(AutoResume: false);
        var memory = StepWith(off, UsageMemory.Empty, _now, Limited("api", Session(_reset))).Memory;

        var step = StepWith(off, memory, _reset.AddMinutes(5), Limited("api", Session(_reset)));

        Assert.Equal(["USAGE OK api"], step.Lines);
        Assert.Empty(step.Resume);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_unknown_reset_is_probed_every_probe_interval_and_a_resume_that_did_not_take_is_tried_again()
    {
        var stop = Session(resets: null);
        var memory = Step(UsageMemory.Empty, _now, Limited("api", stop)).Memory;

        var early = Step(memory, _now.AddMinutes(14), Limited("api", stop));
        var probe = Step(memory, _now.AddMinutes(15), Limited("api", stop));
        var soon = Step(probe.Memory, _now.AddMinutes(29), Limited("api", stop));
        var again = Step(probe.Memory, _now.AddMinutes(30), Limited("api", stop));

        Assert.Empty(early.Lines);
        Assert.Equal(["USAGE OK api"], probe.Lines);
        Assert.Equal(["api"], probe.Resume);
        Assert.Empty(soon.Lines);
        Assert.Equal(["USAGE OK api"], again.Lines);
        Assert.Equal(["api"], again.Resume);
        Assert.Equal("USAGE LIMITED api session resets unknown", Step(UsageMemory.Empty, _now, Limited("api", stop)).Lines[0]);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_probe_interval_comes_from_the_config()
    {
        var stop = Session(resets: null);
        var config = new UsageConfig(ProbeMinutes: 5);
        var memory = StepWith(config, UsageMemory.Empty, _now, Limited("api", stop)).Memory;

        Assert.Equal(["USAGE OK api"], StepWith(config, memory, _now.AddMinutes(5), Limited("api", stop)).Lines);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_lane_that_recovers_by_itself_is_announced_ok_and_forgotten_but_one_we_resumed_is_forgotten_quietly()
    {
        var memory = Step(UsageMemory.Empty, _now, Limited("api", Session(_reset))).Memory;
        var resumed = Step(memory, _reset.AddMinutes(5), Limited("api", Session(_reset))).Memory;

        var byItself = Step(memory, _now.AddHours(1), Ok("api"));
        var afterResume = Step(resumed, _reset.AddMinutes(6), Ok("api"));

        Assert.Equal(["USAGE OK api"], byItself.Lines);
        Assert.Empty(byItself.Memory.Entries);
        Assert.Empty(afterResume.Lines);
        Assert.Empty(afterResume.Memory.Entries);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_different_limit_after_a_resume_is_announced_as_new()
    {
        var memory = Step(UsageMemory.Empty, _now, Limited("api", Session(_reset))).Memory;
        var resumed = Step(memory, _reset.AddMinutes(5), Limited("api", Session(_reset))).Memory;
        var weekly = new UsageStop(UsageKind.Weekly, new DateTimeOffset(2026, 10, 20, 5, 0, 0, TimeSpan.Zero), "weekly");

        var step = Step(resumed, _reset.AddMinutes(9), Limited("api", weekly));

        Assert.Equal(["USAGE LIMITED api weekly resets 2026-10-20 05:00:00Z"], step.Lines);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_unreadable_subject_keeps_what_was_remembered()
    {
        var memory = Step(UsageMemory.Empty, _now, Limited("api", Session(_reset))).Memory;

        var step = Step(memory, _now.AddMinutes(10), new UsageReading("api", UsageState.Unknown, Stop: null, Idle: false, "no pane"));

        Assert.Empty(step.Lines);
        Assert.Equal(memory.Entries["api"], step.Memory.Entries["api"]);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_meta_is_woken_by_two_lines_and_never_typed_into()
    {
        var memory = Step(UsageMemory.Empty, _now, Limited("meta", Session(_reset), idle: true)).Memory;

        var step = Step(memory, _reset.AddMinutes(3), Limited("meta", Session(_reset), idle: true));

        Assert.Equal(["USAGE OK meta", "USAGE OK meta: resume your subagents (SendMessage them) and re-arm the watch"], step.Lines);
        Assert.Empty(step.Resume);
        Assert.Empty(Step(step.Memory, _reset.AddMinutes(4), Limited("meta", Session(_reset))).Lines);
    }

    [Fact, Trait("Lane", "unit")]
    public void Subjects_are_independent()
    {
        var step = Step(UsageMemory.Empty, _now, Limited("api", Session(_reset)), Ok("ui"), Limited("meta", Session(_reset)));

        Assert.Equal(["USAGE LIMITED api session resets 2026-10-19 09:50:00Z", "USAGE LIMITED meta session resets 2026-10-19 09:50:00Z"], step.Lines);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("garbage\nmore\tgarbage\t\t")]
    public void Damaged_memory_reads_as_empty(string? text) =>
        Assert.Empty(UsageMemory.Parse(text).Entries);

    [Fact, Trait("Lane", "unit")]
    public void The_resume_line_is_the_one_the_owner_asked_for() =>
        Assert.Equal("continue — the usage window has reset; resume where you stopped, including paused builders", UsageWatch.ResumeLine);
}
