using System.Net;
using System.Net.Sockets;
using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Onyx;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxStatus;

// The real adapter over a temp directory and real loopback listeners.
[Lane(TestLane.Integration)]
public sealed class HostProbesContractTests : HostProbesContract
{
    protected override Scene Create(out IDisposable holder)
    {
        var directory = Directory.CreateTempSubdirectory("aw-host-probes-");
        var taken = new TcpListener(IPAddress.Loopback, 0);
        taken.Start();
        var takenPort = ((IPEndPoint)taken.LocalEndpoint).Port;
        var free = new TcpListener(IPAddress.Loopback, 0);
        free.Start();
        var freePort = ((IPEndPoint)free.LocalEndpoint).Port;
        free.Stop();   // free: released, so the probe can bind it
        holder = new Scope(taken, directory);
        return new Scene(takenPort, freePort, directory.FullName, new HostProbes());
    }

    private sealed class Scope(TcpListener taken, DirectoryInfo directory) : IDisposable
    {
        public void Dispose()
        {
            taken.Stop();
            directory.Delete(recursive: true);
        }
    }
}
