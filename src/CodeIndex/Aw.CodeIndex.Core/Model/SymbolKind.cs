using System.Text.Json.Serialization;

namespace Aw.CodeIndex.Core.Model;

/// <summary>What a symbol is (spec §3); 'graph-symbol' is a symbol graphify named in a language no parser covers.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SymbolKind>))]
public enum SymbolKind
{
    /// <summary>A free function.</summary>
    [JsonStringEnumMemberName("function")]
    Function,

    /// <summary>A method.</summary>
    [JsonStringEnumMemberName("method")]
    Method,

    /// <summary>A constructor.</summary>
    [JsonStringEnumMemberName("constructor")]
    Constructor,

    /// <summary>A property accessor.</summary>
    [JsonStringEnumMemberName("accessor")]
    Accessor,

    /// <summary>A local function.</summary>
    [JsonStringEnumMemberName("local-function")]
    LocalFunction,

    /// <summary>A lambda that is assigned a name.</summary>
    [JsonStringEnumMemberName("lambda")]
    Lambda,

    /// <summary>A class.</summary>
    [JsonStringEnumMemberName("class")]
    Class,

    /// <summary>A record.</summary>
    [JsonStringEnumMemberName("record")]
    Record,

    /// <summary>A struct.</summary>
    [JsonStringEnumMemberName("struct")]
    Struct,

    /// <summary>An interface.</summary>
    [JsonStringEnumMemberName("interface")]
    Interface,

    /// <summary>An enum.</summary>
    [JsonStringEnumMemberName("enum")]
    Enum,

    /// <summary>A node graphify named; no parser, no clone rows.</summary>
    [JsonStringEnumMemberName("graph-symbol")]
    GraphSymbol,
}
