namespace Aw.Packets.App.Commands;

/// <summary>The arguments 'hash', 'verify' and the scoping commands share: &lt;packet-dir&gt; [--json] and any value flags and switches the command names.</summary>
/// <param name="PacketDirectory">The packet directory.</param>
/// <param name="Json">True for '--json'.</param>
/// <param name="Values">Each value flag given, with its value.</param>
/// <param name="Switches">Each switch (a flag without a value) given.</param>
internal sealed record SimpleArguments(string PacketDirectory, bool Json, IReadOnlyDictionary<string, string> Values, IReadOnlySet<string> Switches)
{
    /// <summary>Binds the arguments.</summary>
    /// <param name="args">The arguments after the command name and '--kind'.</param>
    /// <param name="valueFlags">The flags that take a value, each allowed once.</param>
    /// <returns>The arguments; null for an unknown, repeated or valueless flag, or no (or a second) packet directory.</returns>
    internal static SimpleArguments? Parse(IReadOnlyList<string> args, params string[] valueFlags) => Parse(args, switches: [], valueFlags);

    /// <summary>Binds the arguments, with switches.</summary>
    /// <param name="args">The arguments after the command name and '--kind'.</param>
    /// <param name="switches">The flags that take no value, each allowed once.</param>
    /// <param name="valueFlags">The flags that take a value, each allowed once.</param>
    /// <returns>The arguments; null for an unknown, repeated or valueless flag, or no (or a second) packet directory.</returns>
    internal static SimpleArguments? Parse(IReadOnlyList<string> args, string[] switches, params string[] valueFlags)
    {
        var bound = new Binding(switches, valueFlags);
        for (var i = 0; i < args.Count; i++)
        {
            if (bound.Take(args, i) is not { } consumed)
            {
                return null;
            }

            i += consumed - 1;
        }

        return bound.Packet is null ? null : new SimpleArguments(bound.Packet, bound.Json, bound.Values, bound.Given);
    }

    private sealed class Binding(string[] switches, string[] valueFlags)
    {
        internal string? Packet { get; private set; }

        internal bool Json { get; private set; }

        internal Dictionary<string, string> Values { get; } = new(StringComparer.Ordinal);

        internal HashSet<string> Given { get; } = new(StringComparer.Ordinal);

        /// <summary>Binds the argument at <paramref name="at"/>; returns how many arguments it used, or null when it isn't allowed here.</summary>
        internal int? Take(IReadOnlyList<string> args, int at) => TakeFlag(args, at) ?? TakePacket(args[at]);

        private int? TakeFlag(IReadOnlyList<string> args, int at)
        {
            var arg = args[at];
            if (arg == "--json" && !Json)
            {
                Json = true;
                return 1;
            }

            if (switches.Contains(arg, StringComparer.Ordinal) && Given.Add(arg))
            {
                return 1;
            }

            return valueFlags.Contains(arg, StringComparer.Ordinal) && at + 1 < args.Count && args[at + 1].Length > 0 && Values.TryAdd(arg, args[at + 1]) ? 2 : null;
        }

        private int? TakePacket(string arg)
        {
            if (arg.Length == 0 || Packet is not null || arg.StartsWith("--", StringComparison.Ordinal))
            {
                return null;
            }

            Packet = arg;
            return 1;
        }
    }
}
