namespace Aw.Packets.App.Commands;

/// <summary>The arguments 'hash' and the scoping commands share: &lt;packet-dir&gt; [--json] and any value flags the command names.</summary>
/// <param name="PacketDirectory">The packet directory.</param>
/// <param name="Json">True for '--json'.</param>
/// <param name="Values">Each value flag given, with its value.</param>
internal sealed record SimpleArguments(string PacketDirectory, bool Json, IReadOnlyDictionary<string, string> Values)
{
    /// <summary>Binds the arguments.</summary>
    /// <param name="args">The arguments after the command name and '--kind'.</param>
    /// <param name="valueFlags">The flags that take a value, each allowed once.</param>
    /// <returns>The arguments; null for an unknown, repeated or valueless flag, or no (or a second) packet directory.</returns>
    internal static SimpleArguments? Parse(IReadOnlyList<string> args, params string[] valueFlags)
    {
        string? packet = null;
        var json = false;
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < args.Count; i++)
        {
            if (args[i] == "--json" && !json)
            {
                json = true;
            }
            else if (TakeValue(args, i, valueFlags, values))
            {
                i++;
            }
            else if (args[i].Length > 0 && packet is null && !args[i].StartsWith("--", StringComparison.Ordinal))
            {
                packet = args[i];
            }
            else
            {
                return null;
            }
        }

        return packet is null ? null : new SimpleArguments(packet, json, values);
    }

    private static bool TakeValue(IReadOnlyList<string> args, int at, string[] valueFlags, Dictionary<string, string> values) =>
        valueFlags.Contains(args[at], StringComparer.Ordinal) && at + 1 < args.Count && args[at + 1].Length > 0 && values.TryAdd(args[at], args[at + 1]);
}
