using Aw.Context.Core.Locks;
using Aw.Kernel;
using Aw.Sindri.Core.Locks;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// The context engine's heavy-job lock (context spec §4), over Sindri's own port <see cref="IHeavySlots"/> (the shared lock directory,
/// so builds, evidence runs and the shell tools count against one cap). A lock that stays busy, or a lock directory that can't be
/// used, is SND-CONTEXT-040: a map build never runs without its slot.
/// </summary>
internal sealed class SindriHeavyJobLock(IHeavySlots slots, TimeSpan wait) : IHeavyJobLock
{
    /// <summary>How long a build waits for a slot before it answers 040.</summary>
    internal static TimeSpan DefaultWait { get; } = TimeSpan.FromSeconds(60);

    public ValueTask<Result<IAsyncDisposable>> Take(IProgress<string>? waiting, CancellationToken cancellationToken) => throw new NotSupportedException($"{slots}{wait}");
}
