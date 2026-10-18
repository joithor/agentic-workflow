using System.Globalization;
using System.Text;
using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Infrastructure.Sandbox;

#pragma warning disable MA0182 // until sandbox launcher (SB-5a) starts it
/// <summary>The inside half of §3.2: the init shell that starts both forwarders, then runs the agent.</summary>
internal static class EgressInit
{
    /// <summary>Where the net directory is bound inside the sandbox.</summary>
    public const string InsideDirectory = "/run/sindri/net";

    /// <summary>The loopback names the proxy must never see.</summary>
    public const string NoProxy = "127.0.0.1,localhost,::1";

    /// <summary>Renders the init script; its arguments are the agent's argv. The agent runs as a child so a trap stops the forwarders with it.</summary>
    /// <param name="network">The plan's network.</param>
    /// <param name="socat">The socat to run (a name or path).</param>
    /// <returns>The script text.</returns>
    public static string Render(PlanNetwork network, string socat)
    {
        ArgumentNullException.ThrowIfNull(network);
        ArgumentNullException.ThrowIfNull(socat);
        var script = new StringBuilder();
        _ = script.Append("#!/bin/sh\n")
            .Append("# Sindri sandbox init (sandbox spec 3.2): start both forwarders, then run the agent.\n")
            .Append("exec 3>&- 4>&- 5>&- 6>&- 7>&- 8>&- 9>&-\n")
            .Append(Forward(socat, network.ProxyPort, "proxy.sock")).Append("proxy=$!\n")
            .Append(Forward(socat, network.SindriPort, "sindri.sock")).Append("sindri=$!\n")
            .Append("trap 'kill \"$proxy\" \"$sindri\" 2>/dev/null' EXIT\n")
            .Append("export NO_PROXY='").Append(NoProxy).Append("' no_proxy='").Append(NoProxy).Append("'\n")
            .Append("\"$@\"\n")
            .Append("status=$?\n")
            .Append("exit \"$status\"\n");
        return script.ToString();
    }

    private static string Forward(string socat, int port, string socket) =>
        string.Create(CultureInfo.InvariantCulture, $"'{socat}' TCP-LISTEN:{port},bind=127.0.0.1,fork,reuseaddr UNIX-CONNECT:{InsideDirectory}/{socket} &\n");
}
#pragma warning restore MA0182
