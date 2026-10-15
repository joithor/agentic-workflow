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
    public async Task A_slot_held_by_a_live_process_without_an_owner_line_stays_held()
    {
        Directory.CreateDirectory(Lock);

        var taken = (await Slots(slots: 1).Take("x", TimeSpan.Zero, waiting: null, TestContext.Current.CancellationToken)).Value;

        Assert.Null(taken.Lease);
        Assert.Contains("an unknown holder", taken.Busy, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Waiting_tells_the_progress_once_and_gives_up_at_the_timeout()
    {
        Directory.CreateDirectory(Lock);
        await File.WriteAllTextAsync(Path.Combine(Lock, "owner"), $"{Pid} 2026-10-01T00:00:00Z other\n", TestContext.Current.CancellationToken);
        var told = new List<string>();
        var clock = new Microsoft.Extensions.Time.Testing.FakeTimeProvider();
        var slots = new DirectoryHeavySlots(new HeavyLockSettings(Lock, 1, Held: false), clock);

        var waiting = slots.Take("x", TimeSpan.FromSeconds(3), new Progress<string>(told.Add), TestContext.Current.CancellationToken).AsTask();
        while (!waiting.IsCompleted)
        {
            clock.Advance(TimeSpan.FromSeconds(1));
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        var taken = (await waiting).Value;
        Assert.Null(taken.Lease);
        Assert.Contains("waiting for heavy-job lock (slot 1/1 held by " + Pid, Assert.Single(told), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_settings_read_the_variables_the_shell_tools_read()
    {
        var plain = HeavyLockSettings.Read(_ => null, "/state");
        var set = HeavyLockSettings.Read(name => name switch { "AW_HEAVY_JOB_LOCK" => "/x/lock", "AW_HEAVY_SLOTS" => "3", "AW_HEAVY_LOCK_HELD" => "1", _ => null }, "/state");
        var bad = HeavyLockSettings.Read(name => name == "AW_HEAVY_SLOTS" ? "zero" : null, "/state");

        Assert.Equal(("/state/locks/heavy-job.lock", 2, false), (plain.Lock, plain.Slots, plain.Held));
        Assert.Equal(("/x/lock", 3, true), (set.Lock, set.Slots, set.Held));
        Assert.Equal(2, bad.Slots);
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
