using System.Globalization;
using Aw.Context.Core.Locks;
using Aw.Context.Testing.Locks;
using Aw.Kernel.Testing;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Infrastructure.Locks;

namespace Aw.Sindri.Tests.Locks;

/// <summary>The context engine's lock contract against the real adapter: Sindri's lock port over directories on a temp path.</summary>
[Trait("Lane", "integration")]
public sealed class DirectoryHeavySlotsContractTests : HeavyJobLockContract, IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("aw-heavy-contract-").FullName;
    private int _locks;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    protected override IHeavyJobLock CreateLock(string? busyHolder, int capacity)
    {
        var path = Path.Combine(_root, string.Create(CultureInfo.InvariantCulture, $"lock-{++_locks}"), "heavy-job.lock");
        if (busyHolder is not null)
        {
            for (var k = 1; k <= capacity; k++)
            {
                var slot = k == 1 ? path : string.Create(CultureInfo.InvariantCulture, $"{path}.{k}");
                _ = Directory.CreateDirectory(slot);
                File.WriteAllText(Path.Combine(slot, DirectoryHeavySlots.OwnerFile), string.Create(CultureInfo.InvariantCulture, $"{Environment.ProcessId} {RealTime.NowDateTime:yyyy-MM-ddTHH:mm:ssZ} {busyHolder}\n"));
            }
        }

        return new SindriHeavyJobLock(new DirectoryHeavySlots(new HeavyLockSettings(path, capacity, Held: false), TimeProvider.System), TimeSpan.Zero);
    }
}
