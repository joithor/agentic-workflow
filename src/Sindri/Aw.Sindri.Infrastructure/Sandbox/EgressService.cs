using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Infrastructure.Sandbox;

#pragma warning disable MA0182 // until sandbox launcher (SB-5a) starts it
/// <summary>The outside half of §3.2: the socket directory, the Sindri forward and the proxy, for one launch.</summary>
/// <param name="resolver">Resolves names for the proxy.</param>
/// <param name="dialer">Opens outbound connections.</param>
/// <param name="environment">Reads an environment variable (AW_SANDBOX_SOCAT, PATH).</param>
/// <param name="runDirectory">Where egress.log goes.</param>
internal sealed class EgressService(IEgressResolver resolver, IEgressDialer dialer, Func<string, string?> environment, string runDirectory)
{
    /// <summary>Starts the listeners.</summary>
    /// <param name="network">The plan's network (its NetDir is created when missing).</param>
    /// <param name="caps">The caps.</param>
    /// <returns>The running egress, or -014 no-forwarder, or -016 net-forward when the socket paths don't fit.</returns>
    public Result<RunningEgress> Start(PlanNetwork network, EgressCaps caps)
    {
        ArgumentNullException.ThrowIfNull(network);
        ArgumentNullException.ThrowIfNull(caps);
        if (FindSocat() is not { } socat)
        {
            return SandboxRefusal.For(SindriErrors.LaunchSandboxUnavailable, SandboxReasons.NoForwarder, "socat was not found");
        }

        if (!EgressSocketDirectory.SocketPathsFit(network.NetDir))
        {
            return SandboxRefusal.For(SindriErrors.LaunchSelfCheckFailed, SandboxReasons.NetForward, "the socket directory path is too long");
        }

        EgressSocketDirectory.Create(network.NetDir);
        _ = Directory.CreateDirectory(runDirectory);
        var log = new EgressLog(Path.Combine(runDirectory, "egress.log"));
        var proxy = new EgressProxy(new EgressPolicy(network.Groups, resolver), dialer, log, caps);
        var forward = new EgressForward(network.SindriPort);
#pragma warning disable CA2000 // ownership passes to RunningEgress, which disposes both
        var proxyListener = EgressListener.Start(Path.Combine(network.NetDir, EgressSocketDirectory.ProxySocket), proxy.Handle);
        try
        {
            var sindriListener = EgressListener.Start(Path.Combine(network.NetDir, EgressSocketDirectory.SindriSocket), forward.Handle);
            log.Flush();
            return new RunningEgress(socat, network.NetDir, log, proxyListener, sindriListener);
        }
        catch
        {
            _ = proxyListener.DisposeAsync().AsTask();
            throw;
        }
#pragma warning restore CA2000
    }

    private string? FindSocat()
    {
        if (environment("AW_SANDBOX_SOCAT") is { Length: > 0 } configured)
        {
            return File.Exists(configured) ? configured : null;
        }

        return (environment("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(d => Path.Combine(d, "socat")).FirstOrDefault(File.Exists);
    }
}
#pragma warning restore MA0182
