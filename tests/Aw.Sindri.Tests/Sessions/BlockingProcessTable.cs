using Aw.Kernel;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>A process table whose read waits until released: a source that overruns the pass budget.</summary>
internal sealed class BlockingProcessTable : IProcessTable, IDisposable
{
    private readonly ManualResetEventSlim _gate = new(initialState: false);

    public ReadSignal Reads { get; } = new();

    public void Release() => _gate.Set();

    public Result<IReadOnlyList<ProcessEntry>> Read(CancellationToken cancellationToken)
    {
        Reads.Increment();
        _ = _gate.Wait(TimeSpan.FromSeconds(10), CancellationToken.None);
        return Results.Success<IReadOnlyList<ProcessEntry>>([]);
    }

    public IReadOnlyDictionary<string, string> Environment(int pid) => new Dictionary<string, string>(StringComparer.Ordinal);

    public string? Cwd(int pid) => null;

    public IReadOnlyList<string> OpenFiles(int pid) => [];

    public void Dispose() => _gate.Dispose();
}
