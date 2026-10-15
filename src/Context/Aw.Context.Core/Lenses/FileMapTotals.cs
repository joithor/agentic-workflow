
namespace Aw.Context.Core.Lenses;

/// <summary>File counts by status under the path.</summary>
/// <param name="Mapped">Mapped files.</param>
/// <param name="Suggested">Suggested files.</param>
/// <param name="Unmapped">Unmapped files.</param>
public sealed record FileMapTotals(int Mapped, int Suggested, int Unmapped);
