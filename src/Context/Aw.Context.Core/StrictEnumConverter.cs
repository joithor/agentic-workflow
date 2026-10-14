using System.Text.Json.Serialization;

namespace Aw.Context.Core;

/// <summary>
/// A string enum converter that refuses numbers: a map file's 'type: 5' or 'provenance: 7' is a JSON error (so
/// SND-CONTEXT-031 with its path), never a silent member or an undefined value.
/// </summary>
/// <typeparam name="TEnum">The enum.</typeparam>
public sealed class StrictEnumConverter<TEnum> : JsonStringEnumConverter<TEnum>
    where TEnum : struct, Enum
{
    /// <summary>Names only, as each member's JsonStringEnumMemberName says.</summary>
    public StrictEnumConverter()
        : base(namingPolicy: null, allowIntegerValues: false)
    {
    }
}
