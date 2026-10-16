using System.Net;
using System.Net.Sockets;
using Aw.Sindri.Infrastructure.Onyx;

namespace Aw.Sindri.Tests.OnyxStatus;

public sealed class HostProbesTests
{
    [Fact, Trait("Lane", "integration")]
    public void Ports_reports_a_bound_port_as_taken()
    {
        var holder = new TcpListener(IPAddress.Loopback, 0);
        holder.Start();
        try
        {
            var port = ((IPEndPoint)holder.LocalEndpoint).Port;

            var facts = new HostProbes().Ports(port, port, port);

            Assert.False(facts.PreferredFree);
            Assert.Null(facts.FirstFreeFallback);
        }
        finally
        {
            holder.Stop();
        }
    }

    [Fact, Trait("Lane", "integration")]
    public void Ports_reports_a_port_taken_on_every_interface_as_taken()
    {
        // Docker publishes on 0.0.0.0: a bind to 127.0.0.1 beside it must not count as free.
        var holder = new TcpListener(IPAddress.Any, 0) { ExclusiveAddressUse = false };
        holder.Start();
        try
        {
            var port = ((IPEndPoint)holder.LocalEndpoint).Port;

            Assert.False(new HostProbes().Ports(port, port, port).PreferredFree);
        }
        finally
        {
            holder.Stop();
        }
    }

    [Fact, Trait("Lane", "integration")]
    public void FreeDisk_measures_the_nearest_existing_ancestor()
    {
        var directory = Directory.CreateTempSubdirectory("aw-free-disk-");
        try
        {
            var facts = new HostProbes().FreeDisk(Path.Combine(directory.FullName, "not", "yet", "there"));

            Assert.Equal(directory.FullName, facts.MeasuredPath);
            Assert.True(facts.FreeBytes > 0);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
