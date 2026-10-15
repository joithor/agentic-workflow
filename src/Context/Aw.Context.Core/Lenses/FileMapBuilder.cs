using Aw.Context.Core.Graph;
using Aw.Kernel;

namespace Aw.Context.Core.Lenses;

/// <summary>Builds the file map of a directory (spec §8.9, S-4). Pure over an applied graph and a file listing.</summary>
public static class FileMapBuilder
{
    /// <summary>Builds the view.</summary>
    /// <param name="basis">The map version and the commit listed.</param>
    /// <param name="query">What was asked.</param>
    /// <param name="files">Every tracked file of the repo, paths from the root.</param>
    /// <param name="graph">The graph a caller may see: overrides applied.</param>
    /// <returns>The view, or SND-CONTEXT-003 when no file lies under the path.</returns>
    public static Result<FileMapView> Build(FileMapBasis basis, FileMapQuery query, IReadOnlyList<string> files, GraphSnapshot graph) =>
        throw new NotSupportedException();
}
