using System.Text.Json.Serialization;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Packets.Core.Ids;

/// <summary>PacketId: the id of a packet (lower-case letters, digits and '-', at most 64, so it is safe as a directory name under packets/).</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<PacketId>))]
public readonly partial struct PacketId : ITypedId<PacketId, string>
{
    private static Validation Validate(string value) =>
        IsPathSafe(value) ? Validation.Ok : Validation.Invalid("PacketId must be 1-64 lower-case letters, digits or '-', starting with a letter or digit");

    private static bool IsPathSafe(string value)
    {
        if (value.Length is 0 or > 64 || value[0] == '-')
        {
            return false;
        }

        foreach (var c in value)
        {
            if (c is not ((>= 'a' and <= 'z') or (>= '0' and <= '9') or '-'))
            {
                return false;
            }
        }

        return true;
    }
}
