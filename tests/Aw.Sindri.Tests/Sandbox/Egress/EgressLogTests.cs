using Aw.Sindri.Infrastructure.Sandbox;

namespace Aw.Sindri.Tests.Sandbox.Egress;

public sealed class EgressLogTests
{
    [Fact, Trait("Lane", "integration")]
    public void Egress_log_counts_per_host_without_urls()
    {
        var path = Path.Combine(Directory.CreateTempSubdirectory("egress-log").FullName, "egress.log");
        var log = new EgressLog(path);

        log.Record("API.anthropic.com", 443, EgressVerdict.Allowed);
        log.Record("api.anthropic.com", 443, EgressVerdict.Allowed);
        log.Record("evil.example", 443, EgressVerdict.Denied);
        log.Flush();

        Assert.Equal(2, log.Count("api.anthropic.com", 443, EgressVerdict.Allowed));
        Assert.Equal(["api.anthropic.com\t443\tallowed\t2", "evil.example\t443\tdenied\t1"], File.ReadAllLines(path));
        Assert.False(File.Exists(path + ".tmp"));
    }
}
