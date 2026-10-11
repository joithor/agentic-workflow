using System.Text.Json.Serialization;

namespace Aw.Evidence.Core;

/// <summary>Reads and writes an enum only by its declared names ([JsonStringEnumMemberName]); a number is refused.</summary>
/// <typeparam name="TEnum">The enum.</typeparam>
public sealed class StrictEnumConverter<TEnum>() : JsonStringEnumConverter<TEnum>(namingPolicy: null, allowIntegerValues: false)
    where TEnum : struct, Enum;
