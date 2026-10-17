using System.Text.Json.Serialization;

namespace Aw.Packets.Core.Kinds.Scoping;

/// <summary>A band of the context map a found node sits in (the Context view draws each in its own band).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<LensBand>))]
public enum LensBand
{
    /// <summary>Code nodes and <c>code:&lt;path&gt;</c> file keys.</summary>
    [JsonStringEnumMemberName("code")]
    Code = 1,

    /// <summary>Product nodes.</summary>
    [JsonStringEnumMemberName("product")]
    Product = 2,

    /// <summary>Intent: decisions, tickets and the like.</summary>
    [JsonStringEnumMemberName("intent")]
    Intent = 3,
}

/// <summary>Who stands behind a node, edge or item: <c>untrusted</c> unless the owner's own answer made it.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<LensTrust>))]
public enum LensTrust
{
    /// <summary>Text from outside; the default for everything a lens returned.</summary>
    [JsonStringEnumMemberName("untrusted")]
    Untrusted = 1,

    /// <summary>A decision record with the owner's answer provenance (scoping spec §11).</summary>
    [JsonStringEnumMemberName("owner")]
    Owner = 2,
}

/// <summary>A found node: a map node key or a <c>code:&lt;path&gt;</c> file key.</summary>
/// <param name="Key">The node key.</param>
/// <param name="Kind">The node kind.</param>
/// <param name="Label">What the view shows.</param>
/// <param name="Band">The band it is drawn in.</param>
/// <param name="LensItem">Its <c>L-&lt;lens&gt;-&lt;n&gt;</c> id.</param>
/// <param name="Trust">Its trust label.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LensNode(string Key, string Kind, string Label, LensBand Band, string LensItem, LensTrust Trust);

/// <summary>An edge among found nodes and the anchors, as the lens returned it.</summary>
/// <param name="From">One end (a node key or an anchor).</param>
/// <param name="To">The other end.</param>
/// <param name="Type">The edge type.</param>
/// <param name="Provenance">Where the edge came from.</param>
/// <param name="Confirmed">True when the owner or a review confirmed it.</param>
/// <param name="Trust">Its trust label.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LensEdge(string From, string To, string Type, string Provenance, bool Confirmed, LensTrust Trust);

/// <summary>(LensFound) A found item with no graph key: a commit, a pull request, an incident.</summary>
/// <param name="LensItem">Its <c>L-&lt;lens&gt;-&lt;n&gt;</c> id.</param>
/// <param name="Kind">The item kind.</param>
/// <param name="Label">What the view shows.</param>
/// <param name="Citations">Where it was found (source refs).</param>
/// <param name="Trust">Its trust label.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LensFound(string LensItem, string Kind, string Label, IReadOnlyList<string> Citations, LensTrust Trust);
