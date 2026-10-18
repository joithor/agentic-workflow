using System.Text.Json.Serialization;

namespace Aw.Sindri.Features.ReadInjection;

/// <summary>The scrubbed text of the sections an open run's Step inputs filled (API spec section 3.8).</summary>
/// <param name="Run">The run token the text belongs to.</param>
/// <param name="Sections">The sections, in plan order; each holds its text.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record InjectionView(string Run, IReadOnlyList<InjectionSectionView> Sections);
