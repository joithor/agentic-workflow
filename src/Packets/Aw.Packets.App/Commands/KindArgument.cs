using Aw.Packets.Core.Kinds;

namespace Aw.Packets.App.Commands;

/// <summary>Pulls '--kind &lt;name&gt;' out of a command's arguments; the build kind when it isn't given.</summary>
internal static class KindArgument
{
    /// <summary>Finds and removes '--kind'.</summary>
    /// <param name="args">The arguments after the command name.</param>
    /// <returns>The kind and the other arguments; null for a repeated '--kind', a missing value or an unknown kind.</returns>
    internal static (PacketKind Kind, IReadOnlyList<string> Arguments)? Extract(IReadOnlyList<string> args)
    {
        PacketKind? kind = null;
        var rest = new List<string>();
        for (var i = 0; i < args.Count; i++)
        {
            if (args[i] != "--kind")
            {
                rest.Add(args[i]);
                continue;
            }

            if (kind is not null || i + 1 >= args.Count || PacketKinds.Find(args[i + 1]) is not { } found)
            {
                return null;
            }

            kind = found;
            i++;
        }

        return (kind ?? PacketKinds.Build, rest);
    }
}
