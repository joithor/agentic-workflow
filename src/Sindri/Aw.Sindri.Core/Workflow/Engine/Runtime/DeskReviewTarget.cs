namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>Where a reviewed fact opens, read from the fact's stored value (<see cref="DeskReviewTargets"/>).</summary>
/// <param name="Path">The file or packet directory; null when the value names none.</param>
/// <param name="Url">The link; null when the value names none.</param>
public sealed record DeskReviewTarget(string? Path, string? Url);
