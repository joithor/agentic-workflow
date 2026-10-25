using Aw.Kernel;
using Aw.Sindri.Core.Scoping;

namespace Aw.Sindri.Tests.Workflow.Prove;

/// <summary>An <see cref="IBriefFiles"/> that reads through another and remembers the roots it was asked to read under.</summary>
/// <param name="inner">The reader.</param>
internal sealed class RootRecordingFiles(IBriefFiles inner) : IBriefFiles
{
    /// <summary>The roots of every read, in order.</summary>
    public List<string> Roots { get; } = [];

    /// <inheritdoc/>
    public ValueTask<Result<BriefFile>> Read(string path, IReadOnlyList<string> roots, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(roots);
        Roots.AddRange(roots);
        return inner.Read(path, roots, cancellationToken);
    }
}
