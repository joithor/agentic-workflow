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
        var at = -1;
        for (var i = 0; i < args.Count; i++)
        {
            if (args[i] != "--kind")
            {
                continue;
            }

            if (at >= 0)
            {
                return null;
            }

            at = i;
        }

        if (at < 0)
        {
            return (PacketKinds.Build, [.. args]);
        }

        if (at + 1 >= args.Count || PacketKinds.Find(args[at + 1]) is not { } kind)
        {
            return null;
        }

        var rest = new List<string>(args.Count - 2);
        for (var i = 0; i < args.Count; i++)
        {
            if (i != at && i != at + 1)
            {
                rest.Add(args[i]);
            }
        }

        return (kind, rest);
    }
}
