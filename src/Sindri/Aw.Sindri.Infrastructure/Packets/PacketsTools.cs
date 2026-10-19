using Aw.Sindri.Core.Packets;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Infrastructure.Packets;

/// <summary>The aw-packets port for a caller outside the container (the Host's desk helper): the same invocation the services use.</summary>
public static class PacketsTools
{
    /// <summary>aw-packets over a real process.</summary>
    /// <param name="environment">Reads AW_PACKETS_BIN.</param>
    /// <returns>The port.</returns>
    public static IPackets Create(Func<string, string?> environment) => new AwPacketsTool(new ProcessToolRunner(), environment);
}
