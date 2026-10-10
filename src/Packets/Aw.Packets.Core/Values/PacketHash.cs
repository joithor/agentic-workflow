using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Packets.Core.Values;

/// <summary>PacketHash: a packet's canonical hash, 'sha256:' and 64 lower-case hex digits (Digest.PacketDigest).</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<PacketHash>))]
public readonly partial struct PacketHash : ITypedId<PacketHash, string>
{
    private const string Prefix = "sha256:";

    /// <summary>The 64 hex digits after 'sha256:'.</summary>
    public string Hex => Value[Prefix.Length..];

    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("PacketHash must be 'sha256:' and 64 lower-case hex digits");

    [GeneratedRegex("^sha256:[0-9a-f]{64}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
