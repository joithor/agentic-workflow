using System.Diagnostics;
using Aw.Sindri.Infrastructure.Locks;

namespace Aw.Sindri.Tests.Locks;

/// <summary>The heavy-job lock as directories (context spec §4): slot 1 is the path, slot k is '&lt;path&gt;.k', the same protocol as config/lib/locks.sh and the heavy wrapper.</summary>
public sealed class DirectoryHeavySlotsTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("aw-heavy-slots-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static string Pid => Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private string Lock => Path.Combine(_root, "locks", "heavy-job.lock");

    private DirectoryHeavySlots Slots(int slots = 2, bool held = false) => new(new HeavyLockSettings(Lock, slots, held), TimeProvider.System);

    private static async Task<IAsyncDisposable> Taken(DirectoryHeavySlots slots, string holder = "test")
    {
        var taken = (await slots.Take(holder, TimeSpan.Zero, waiting: null, TestContext.Current.CancellationToken)).Value;
        return taken.Lease ?? throw new InvalidOperationException("busy: " + taken.Busy);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_first_holder_takes_slot_1_the_second_slot_2_and_the_third_finds_both_busy_naming_who_holds_them()
    {
        var slots = Slots();

        await using var first = await Taken(slots, "first job");
        await using var second = await Taken(slots, "second job");
        var third = (await slots.Take("third", TimeSpan.Zero, waiting: null, TestContext.Current.CancellationToken)).Value;

        Assert.True(Directory.Exists(Lock));
        Assert.True(Directory.Exists(Lock + ".2"));
        Assert.Null(third.Lease);
        Assert.Contains("slot 1/2 held by " + Pid, third.Busy, StringComparison.Ordinal);
        Assert.Contains("first job", third.Busy, StringComparison.Ordinal);
        Assert.Contains("slot 2/2", third.Busy, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_owner_file_holds_pid_time_and_who()
    {
        var slots = Slots();

        await using var lease = await Taken(slots, "sindri context-map-build");

        var owner = (await File.ReadAllTextAsync(Path.Combine(Lock, "owner"), TestContext.Current.CancellationToken)).Trim().Split(' ', 3);
        Assert.Equal((Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture), "sindri context-map-build"), (owner[0], owner[2]));
        Assert.Matches(@"^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\dZ$", owner[1]);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Releasing_frees_the_slot_for_the_next_holder_and_twice_is_harmless()
    {
        var slots = Slots(slots: 1);
        var first = await Taken(slots);

        await first.DisposeAsync();
        await first.DisposeAsync();
        await using var again = await Taken(slots);

        Assert.True(Directory.Exists(Lock));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_parent_that_holds_a_slot_makes_taking_one_a_no_op()
    {
        var slots = Slots(held: true);

        await using var lease = await Taken(slots);

        Assert.False(Directory.Exists(Lock));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_slot_whose_owner_process_is_gone_is_reclaimed_like_the_heavy_wrapper_does()
    {
        Directory.CreateDirectory(Lock);
        await File.WriteAllTextAsync(Path.Combine(Lock, "owner"), "2147483646 2026-10-01T00:00:00Z dead job\n", TestContext.Current.CancellationToken);

        await using var lease = await Taken(Slots(slots: 1));

        Assert.Contains(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture), await File.ReadAllTextAsync(Path.Combine(Lock, "owner"), TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_slot_with_no_owner_record_stays_held_however_old_it_is_because_locks_sh_and_ui_evidence_write_none()
    {
        Directory.CreateDirectory(Lock);
        Directory.SetLastWriteTimeUtc(Lock, DateTime.UtcNow.AddMinutes(-5));

        var taken = (await Slots(slots: 1).Take("x", TimeSpan.Zero, waiting: null, TestContext.Current.CancellationToken)).Value;

        Assert.Null(taken.Lease);
        Assert.Contains("an unknown holder", taken.Busy, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_slot_with_no_record_is_taken_only_after_the_six_hour_last_resort_the_TS_sindri_uses()
    {
        Directory.CreateDirectory(Lock);
        Directory.SetLastWriteTimeUtc(Lock, DateTime.UtcNow.AddHours(-7));

        await using var lease = await Taken(Slots(slots: 1));

        Assert.Contains(Pid, await File.ReadAllTextAsync(Path.Combine(Lock, "owner"), TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_TS_sindri_holder_record_with_a_live_pid_keeps_the_slot_held()
    {
        Directory.CreateDirectory(Lock);
        await File.WriteAllTextAsync(Lock + ".holder.json", $"{{\"kind\":\"index-build:x\",\"pid\":{Pid},\"host\":\"{Environment.MachineName}\",\"startedAt\":\"2026-10-01T00:00:00Z\"}}", TestContext.Current.CancellationToken);

        var taken = (await Slots(slots: 1).Take("x", TimeSpan.Zero, waiting: null, TestContext.Current.CancellationToken)).Value;

        Assert.Null(taken.Lease);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_TS_sindri_holder_record_with_a_dead_pid_on_this_host_is_reclaimed()
    {
        Directory.CreateDirectory(Lock);
        await File.WriteAllTextAsync(Lock + ".holder.json", $"{{\"kind\":\"index-build:x\",\"pid\":2147483646,\"host\":\"{Environment.MachineName}\",\"startedAt\":\"2026-10-01T00:00:00Z\"}}", TestContext.Current.CancellationToken);
        File.SetLastWriteTimeUtc(Lock + ".holder.json", DateTime.UtcNow.AddMinutes(1));

        await using var lease = await Taken(Slots(slots: 1));

        Assert.Contains(Pid, await File.ReadAllTextAsync(Path.Combine(Lock, "owner"), TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_dead_pid_recorded_on_another_host_is_not_ours_to_judge()
    {
        Directory.CreateDirectory(Lock);
        await File.WriteAllTextAsync(Lock + ".holder.json", "{\"kind\":\"k\",\"pid\":2147483646,\"host\":\"some-other-box\",\"startedAt\":\"2026-10-01T00:00:00Z\"}", TestContext.Current.CancellationToken);
        File.SetLastWriteTimeUtc(Lock + ".holder.json", DateTime.UtcNow.AddMinutes(1));

        var taken = (await Slots(slots: 1).Take("x", TimeSpan.Zero, waiting: null, TestContext.Current.CancellationToken)).Value;

        Assert.Null(taken.Lease);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_owner_write_fails_the_take_and_releases_the_slot()
    {
        // A read-only slot directory makes the owner write fail, as a full disk would.
        var slots = new DirectoryHeavySlots(new HeavyLockSettings(Lock, 1, Held: false), TimeProvider.System)
        {
            AfterMkdir = directory =>
            {
                if (!OperatingSystem.IsWindows())
                {
                    File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserExecute);
                }
            },
        };

        var taken = await slots.Take("x", TimeSpan.Zero, waiting: null, TestContext.Current.CancellationToken);

        Assert.True(taken.IsError);
        Assert.False(Directory.Exists(Lock));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Two_waiters_that_both_judged_a_dead_slot_stale_leave_the_slot_to_the_one_that_retook_it()
    {
        // B read the dead owner; before B takes the slot over, A reclaimed it and holds it now. B must leave A's slot alone.
        Directory.CreateDirectory(Lock);
        await File.WriteAllTextAsync(Path.Combine(Lock, "owner"), "2147483646 2026-10-01T00:00:00Z dead job\n", TestContext.Current.CancellationToken);
        var aOwner = $"{Pid} 2026-10-02T00:00:00Z a's job\n";
        var slots = new DirectoryHeavySlots(new HeavyLockSettings(Lock, 1, Held: false), TimeProvider.System)
        {
            BeforeTakeover = directory =>
            {
                Directory.Delete(directory, recursive: true);
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, "owner"), aOwner);
            },
        };

        var b = (await slots.Take("b's job", TimeSpan.Zero, waiting: null, TestContext.Current.CancellationToken)).Value;

        Assert.Null(b.Lease);
        Assert.Equal(aOwner, await File.ReadAllTextAsync(Path.Combine(Lock, "owner"), TestContext.Current.CancellationToken));
        Assert.Empty(Directory.GetFiles(Lock, "owner.reclaim.*"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Releasing_a_slot_whose_owner_line_names_another_process_leaves_that_holders_slot_alone()
    {
        var slots = Slots(slots: 1);
        var ours = await Taken(slots);
        await File.WriteAllTextAsync(Path.Combine(Lock, "owner"), "2147483646 2026-10-01T00:00:00Z other\n", TestContext.Current.CancellationToken);   // someone removed ours and another job took the slot

        await ours.DisposeAsync();

        Assert.True(Directory.Exists(Lock));
        Assert.StartsWith("2147483646 ", await File.ReadAllTextAsync(Path.Combine(Lock, "owner"), TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Waiting_tells_the_progress_once_and_gives_up_at_the_timeout()
    {
        Directory.CreateDirectory(Lock);
        await File.WriteAllTextAsync(Path.Combine(Lock, "owner"), $"{Pid} 2026-10-01T00:00:00Z other\n", TestContext.Current.CancellationToken);
        var told = new SyncProgress();
        var clock = new Microsoft.Extensions.Time.Testing.FakeTimeProvider();
        var slots = new DirectoryHeavySlots(new HeavyLockSettings(Lock, 1, Held: false), clock);

        var waiting = slots.Take("x", TimeSpan.FromSeconds(3), told, TestContext.Current.CancellationToken).AsTask();
        while (!waiting.IsCompleted)
        {
            clock.Advance(TimeSpan.FromSeconds(1));
            await Task.Yield();
        }

        var taken = (await waiting).Value;
        Assert.Null(taken.Lease);
        Assert.Contains("waiting for heavy-job lock (slot 1/1 held by " + Pid, Assert.Single(told.Lines), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_settings_read_the_variables_the_shell_tools_read()
    {
        var plain = HeavyLockSettings.Read(_ => null, "/state");
        var set = HeavyLockSettings.Read(name => name switch { "AW_HEAVY_JOB_LOCK" => "/x/lock", "AW_HEAVY_SLOTS" => "3", "AW_HEAVY_LOCK_HELD" => "1", _ => null }, "/state");

        Assert.Equal(("/state/locks/heavy-job.lock", 2, false), (plain.Lock, plain.Slots, plain.Held));
        Assert.Equal(("/x/lock", 3, true), (set.Lock, set.Slots, set.Held));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(null, 2)]
    [InlineData("", 2)]
    [InlineData("1", 1)]
    [InlineData("3", 3)]
    [InlineData("03", 3)]
    [InlineData("0", 1)]
    [InlineData("zero", 1)]
    [InlineData("-2", 1)]
    [InlineData("2.5", 1)]
    public void The_slot_count_is_AW_HEAVY_SLOTS_when_set_2_when_unset_or_empty_and_1_when_malformed_as_config_lib_locks_sh_reads_it(string? value, int expected)
    {
        var settings = HeavyLockSettings.Read(name => name == "AW_HEAVY_SLOTS" ? value : null, "/state");

        Assert.Equal(expected, settings.Slots);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_lock_read_from_AW_HEAVY_SLOTS_3_takes_three_holders_and_names_slot_3_of_3_to_the_fourth()
    {
        var settings = HeavyLockSettings.Read(name => name switch { "AW_HEAVY_SLOTS" => "3", "AW_HEAVY_JOB_LOCK" => Lock, _ => null }, _root);
        var slots = new DirectoryHeavySlots(settings, TimeProvider.System);

        await using var one = await Taken(slots);
        await using var two = await Taken(slots);
        await using var three = await Taken(slots);
        var fourth = (await slots.Take("fourth", TimeSpan.Zero, waiting: null, TestContext.Current.CancellationToken)).Value;

        Assert.Null(fourth.Lease);
        Assert.Contains("slot 3/3", fourth.Busy, StringComparison.Ordinal);
        Assert.True(Directory.Exists(Lock + ".3"));
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task It_shares_the_lock_directory_with_config_lib_locks_sh_in_both_directions()
    {
        var ct = TestContext.Current.CancellationToken;
        var script = Path.Combine(FindRepoRoot(), "config", "lib", "locks.sh");
        var slots = Slots();
        await using var ours = await Taken(slots);
        var bash = Run($". '{script}'; slot=\"$(acquire_any_lock '{Lock}' 2 0)\" && echo \"$slot\"");

        var bashSlot = (await bash.StandardOutput.ReadToEndAsync(ct)).Trim();
        await bash.WaitForExitAsync(ct);
        var busy = (await slots.Take("third", TimeSpan.Zero, waiting: null, ct)).Value;

        Assert.Equal(Lock + ".2", bashSlot);   // slot 1 was ours, so the shell took slot 2
        Assert.Null(busy.Lease);
        await ours.DisposeAsync();
        var again = await Taken(slots);   // slot 1 is free again, the shell's slot 2 still stands
        await again.DisposeAsync();
        Assert.True(Directory.Exists(Lock + ".2"));
    }

    private static Process Run(string script)
    {
        var info = new ProcessStartInfo("bash") { RedirectStandardOutput = true, UseShellExecute = false };
        info.ArgumentList.Add("-c");
        info.ArgumentList.Add(script);
        return Process.Start(info) ?? throw new InvalidOperationException("bash didn't start");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AgenticWorkflow.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("the repo root isn't above " + AppContext.BaseDirectory);
    }
}
