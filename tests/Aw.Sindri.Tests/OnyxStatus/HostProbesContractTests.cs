using System.Net;
using System.Net.Sockets;
using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Onyx;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxStatus;

// The real adapter over a temp directory and real loopback listeners. Every port the scene calls taken is held by a listener for the
// whole case. The "free" port is held until the case releases it just before the probe; after that another process, or the probe's own
// loopback connect self-connecting on Linux, can make it read taken, so the contract cases that expect it free retry on a fresh scene.
[Lane(TestLane.Integration)]
public sealed class HostProbesContractTests : HostProbesContract
{
    private const int RangeSize = 3;

    protected override Scene Create(out IDisposable holder)
    {
        var directory = Directory.CreateTempSubdirectory("aw-host-probes-");
        var taken = Hold(IPAddress.Loopback, 0);
        var free = Hold(IPAddress.Loopback, 0);
        var range = HoldRange();
        holder = new Scope([taken, free, .. range], directory);
        return new Scene(PortOf(taken), PortOf(free), directory.FullName, new HostProbes(), PortOf(range[0]), PortOf(range[^1]), free.Stop);
    }

    private static int PortOf(TcpListener listener) => ((IPEndPoint)listener.LocalEndpoint).Port;

    private static TcpListener Hold(IPAddress address, int port)
    {
        var listener = new TcpListener(address, port);
        listener.Start();
        return listener;
    }

    // RangeSize consecutive ports, all bound and held: start from an ephemeral port and retry from another when a neighbour is in use.
    private static List<TcpListener> HoldRange()
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var seed = Hold(IPAddress.Loopback, 0);
            var first = PortOf(seed);
            seed.Stop();
            var held = new List<TcpListener>();
            try
            {
                for (var offset = 0; offset < RangeSize; offset++)
                {
                    held.Add(Hold(IPAddress.Loopback, first + offset));
                }

                return held;
            }
            catch (SocketException)
            {
                held.ForEach(static l => l.Stop());
            }
        }

        throw new InvalidOperationException("no run of free consecutive loopback ports found");
    }

    private sealed class Scope(IReadOnlyList<TcpListener> listeners, DirectoryInfo directory) : IDisposable
    {
        public void Dispose()
        {
            foreach (var listener in listeners)
            {
                listener.Stop();
            }

            directory.Delete(recursive: true);
        }
    }
}
