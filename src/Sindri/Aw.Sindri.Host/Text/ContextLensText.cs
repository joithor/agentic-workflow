using Aw.Context.Core.Lenses;

namespace Aw.Sindri.Host.Text;

/// <summary>Plain text for 'context lenses' and 'context filemap': every status is a word ('suggested', 'unmapped', 'conflict'), never colour.</summary>
internal static class ContextLensText
{
    internal static string Lenses(LensesView view) => view.Map;

    internal static string FileMap(FileMapView view) => view.Map;
}
