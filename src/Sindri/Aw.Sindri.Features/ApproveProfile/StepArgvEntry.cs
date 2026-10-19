using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Features.ApproveProfile;

/// <summary>The entry behind a hash.</summary>
/// <param name="Bin">The program (its directory is not part of the hash).</param>
/// <param name="Open">The open argv template.</param>
/// <param name="Resume">The resume argv template, or null.</param>
/// <param name="Source">Where the entry came from.</param>
public sealed record StepArgvEntry(string Bin, IReadOnlyList<string> Open, IReadOnlyList<string>? Resume, CatalogSource Source);
