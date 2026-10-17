using System.Text.Json.Serialization;

namespace Aw.Packets.Core.Kinds.Scoping;

/// <summary>(LensFound) A found item with no graph key: a commit, a pull request, an incident.</summary>
/// <param name="LensItem">Its 'L-&lt;lens&gt;-&lt;n&gt;' id.</param>
/// <param name="Kind">The item kind.</param>
/// <param name="Label">What the view shows.</param>
/// <param name="Citations">Where it was found (source refs).</param>
/// <param name="Trust">Its trust label.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LensFound(string LensItem, string Kind, string Label, IReadOnlyList<string> Citations, LensTrust Trust);
