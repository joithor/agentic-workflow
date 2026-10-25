
namespace Aw.Context.Core.Map;

/// <summary>What 'context map build' wrote: a draft, with its proposed nodes.</summary>
/// <param name="Draft">The new draft version.</param>
/// <param name="Proposed">The proposed nodes with their provenance, confidence and files.</param>
/// <param name="Degraded">What the build did without ('clusters' when graphify is absent), or empty.</param>
/// <param name="Heads">Where each mapped repo stood when the build read it (the draft's map_version_head rows): the commit and whether the default branch held it. The pre-build 'map heads' read can differ when trunk moves in between.</param>
public sealed record MapBuildReport(MapVersion Draft, IReadOnlyList<ProposedNode> Proposed, IReadOnlyList<string> Degraded, IReadOnlyList<MapHead> Heads);
