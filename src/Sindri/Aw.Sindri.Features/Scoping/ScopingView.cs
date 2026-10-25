using System.Text.Json.Serialization;

namespace Aw.Sindri.Features.Scoping;

/// <summary>
/// The stop's read model (scoping spec §8.4): nine sections in the order the owner reads them, behaviour and blast radius before the
/// ticks. The CLI prints it and the app renders it; neither adds logic.
/// </summary>
/// <param name="Stage">'evidence' once the evidence step has assembled the packet, else 'draft'.</param>
/// <param name="CoverageHeader">The coverage header, for example '2 of 4 lenses limited' (lenses whose limits are not empty, whatever their state).</param>
/// <param name="OpenInContext">U7's 'Open in Context' action.</param>
/// <param name="Sections">The nine sections, in order.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ScopingView(ScopeStage Stage, string CoverageHeader, ScopingOpenInContext OpenInContext, IReadOnlyList<ScopingSection> Sections);
