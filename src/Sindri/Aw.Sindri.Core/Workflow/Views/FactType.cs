using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>The type of a fact row (API spec section 3.8); a type the API doesn't know is served as payload.</summary>
[JsonConverter(typeof(FactTypeJsonConverter))]
public enum FactType
{
    /// <summary>Typed text.</summary>
    [JsonStringEnumMemberName("text")]
    Text = 1,

    /// <summary>A file.</summary>
    [JsonStringEnumMemberName("file")]
    File = 2,

    /// <summary>An item.</summary>
    [JsonStringEnumMemberName("item")]
    Item = 3,

    /// <summary>A link.</summary>
    [JsonStringEnumMemberName("link")]
    Link = 4,

    /// <summary>A scope map.</summary>
    [JsonStringEnumMemberName("scope-map")]
    ScopeMap = 5,

    /// <summary>A packet.</summary>
    [JsonStringEnumMemberName("packet")]
    Packet = 6,

    /// <summary>Evidence packs.</summary>
    [JsonStringEnumMemberName("evidence")]
    Evidence = 7,

    /// <summary>A decision.</summary>
    [JsonStringEnumMemberName("decision")]
    Decision = 8,

    /// <summary>Anything else, shown as a tree.</summary>
    [JsonStringEnumMemberName("payload")]
    Payload = 9,
}
