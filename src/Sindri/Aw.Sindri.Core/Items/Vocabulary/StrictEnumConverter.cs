using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Items.Vocabulary;

/// <summary>A string enum converter that refuses numbers: a request body's '"reason": 1' is a binding error (400 SND-CLI-003), never a silent member or an undefined value.</summary>
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
