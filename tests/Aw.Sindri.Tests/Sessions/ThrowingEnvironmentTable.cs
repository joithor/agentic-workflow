using Aw.Kernel;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>A process table whose per-process reads throw: one source failing mid-pass while the table itself is fine.</summary>
/// <param name="inner">The table behind it.</param>
internal sealed class ThrowingEnvironmentTable(IProcessTable inner) : IProcessTable
{
    public Result<IReadOnlyList<ProcessEntry>> Read(CancellationToken cancellationToken) => inner.Read(cancellationToken);

    public IReadOnlyDictionary<string, string> Environment(int pid) => throw new InvalidOperationException("environ vanished");

    public string? Cwd(int pid) => inner.Cwd(pid);

    public IReadOnlyList<string> OpenFiles(int pid) => inner.OpenFiles(pid);
}
