using Aw.Packets.Core.Model;

namespace Aw.Packets.App.Commands;

/// <summary>The value of '--by': 'owner' or 'machine'.</summary>
internal static class FreezeByArgument
{
    /// <summary>Reads a '--by' value.</summary>
    /// <param name="value">The text after '--by'.</param>
    /// <returns>Who froze it; null for anything but 'owner' or 'machine'.</returns>
    internal static FreezeBy? Parse(string value) => value switch
    {
        "owner" => FreezeBy.Owner,
        "machine" => FreezeBy.Machine,
        _ => null,
    };
}
