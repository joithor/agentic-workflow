using Aw.Kernel;

namespace Aw.Sindri.Core.Scoping;

/// <summary>The brief file 'read-brief' reads (scoping spec §8.2, SND-SCOPING-004): a path inside a root, never under a denied glob, and never through a link that leaves the root.</summary>
public interface IBriefFiles
{
    /// <summary>Reads the file <paramref name="path"/> names under the first root that holds it.</summary>
    /// <param name="path">The path, relative to a root.</param>
    /// <param name="roots">The directories a brief may be in: the repo's checkout and the notes dir.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The file; SND-SCOPING-004 when the path is outside the roots, under a denied glob or through a link that leaves its root; an invalid-input error when it does not exist, no root is given or it is over 1 MiB.</returns>
    public ValueTask<Result<BriefFile>> Read(string path, IReadOnlyList<string> roots, CancellationToken cancellationToken);
}
