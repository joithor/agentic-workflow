namespace Aw.Packets.App.Commands;

/// <summary>The arguments 'validate' and 'freeze' share: &lt;packet-dir&gt; [--repo &lt;dir&gt;] [--json].</summary>
/// <param name="PacketDirectory">The packet directory.</param>
/// <param name="RepoDirectory">The repository directory, when given.</param>
/// <param name="Json">True for '--json'.</param>
internal sealed record PacketArguments(string PacketDirectory, string? RepoDirectory, bool Json)
{
    internal const string Shape = "<packet-dir> [--repo <dir>] [--json]";

    internal static PacketArguments? Parse(IReadOnlyList<string> args)
    {
        string? packet = null;
        string? repo = null;
        var json = false;
        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "--json" when !json:
                    json = true;
                    break;
                case "--repo" when repo is null && i + 1 < args.Count && args[i + 1].Length > 0:
                    repo = args[++i];
                    break;
                case { Length: > 0 } value when packet is null && !value.StartsWith("--", StringComparison.Ordinal):
                    packet = value;
                    break;
                default:
                    return null;
            }
        }

        return packet is null ? null : new PacketArguments(packet, repo, json);
    }
}
