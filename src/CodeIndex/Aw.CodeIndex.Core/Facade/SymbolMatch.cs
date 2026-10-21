using System.Text.Json.Serialization;

namespace Aw.CodeIndex.Core.Facade;

/// <summary>How a symbol query's text is matched (spec §7: exact and range-prefix only, never wildcards).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SymbolMatch>))]
public enum SymbolMatch
{
    /// <summary>The text is a name, path, path#name or ref.</summary>
    [JsonStringEnumMemberName("exact")]
    Exact,

    /// <summary>The text is a name prefix.</summary>
    [JsonStringEnumMemberName("prefix")]
    Prefix,
}
