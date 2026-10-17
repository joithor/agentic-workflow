using System.Globalization;
using Aw.Meta.Core;
using Aw.Meta.Infrastructure.Files;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Adapters;

public sealed class HeavyLockDirectoryTests : IDisposable
{
    private readonly TempDirectory _dir = new();

    private string TakeLock(int pid, DateTime takenAt)
    {
        var slot = Path.Combine(_dir.Path, "heavy-job.lock");
        _ = Directory.CreateDirectory(slot);
        var owner = Path.Combine(slot, "owner");
        File.WriteAllText(owner, string.Create(CultureInfo.InvariantCulture, $"{pid} 2026-10-17T09:00:00Z session=1 dotnet build"));
        File.SetLastWriteTimeUtc(owner, takenAt);
        return Path.Combine(_dir.Path, "heavy-job.lock");
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_holder_pid_that_a_later_process_reused_is_a_stale_lock()
    {
        var path = TakeLock(77, new DateTime(2026, 10, 17, 9, 0, 0, DateTimeKind.Utc));
        var liveness = new InMemoryProcessLiveness();
        liveness.Run(77, new DateTimeOffset(2026, 10, 17, 11, 0, 0, TimeSpan.Zero));

        var locks = await HeavyLockDirectory.List(path, liveness, TestContext.Current.CancellationToken);

        var only = Assert.Single(locks.Value);
        Assert.Equal((77, false), (only.Pid, only.Alive));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_holder_that_started_before_the_lock_was_taken_is_alive()
    {
        var path = TakeLock(77, new DateTime(2026, 10, 17, 9, 0, 0, DateTimeKind.Utc));
        var liveness = new InMemoryProcessLiveness();
        liveness.Run(77, new DateTimeOffset(2026, 10, 17, 8, 0, 0, TimeSpan.Zero));

        var locks = await HeavyLockDirectory.List(path, liveness, TestContext.Current.CancellationToken);

        Assert.True(Assert.Single(locks.Value).Alive);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_host_that_cannot_be_asked_is_the_error()
    {
        var path = TakeLock(77, new DateTime(2026, 10, 17, 9, 0, 0, DateTimeKind.Utc));
        var liveness = new InMemoryProcessLiveness();
        liveness.FailWith(MetaErrors.HostUnreadable.With("unavailable on macOS: ps could not be run"));

        var locks = await HeavyLockDirectory.List(path, liveness, TestContext.Current.CancellationToken);

        Assert.Equal(MetaErrors.HostUnreadable.Code, locks.Error.Code);
    }

    public void Dispose() => _dir.Dispose();
}
