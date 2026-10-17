using System.Text.Json.Serialization;

namespace Aw.Packets.Core.Kinds.Scoping;

/// <summary>A found node: a map node key or a 'code:&lt;path&gt;' file key.</summary>
/// <param name="Key">The node key.</param>
/// <param name="Kind">The node kind.</param>
/// <param name="Label">What the view shows.</param>
/// <param name="Band">The band it is drawn in.</param>
/// <param name="LensItem">Its 'L-&lt;lens&gt;-&lt;n&gt;' id.</param>
/// <param name="Trust">Its trust label.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LensNode(string Key, string Kind, string Label, LensBand Band, string LensItem, LensTrust Trust);
