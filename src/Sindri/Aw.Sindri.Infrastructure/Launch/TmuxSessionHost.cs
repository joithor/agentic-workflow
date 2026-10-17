using System.Diagnostics.CodeAnalysis;
using Aw.Kernel;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>
/// The tmux <see cref="ISessionHost"/> (API P10a, unit U5): sessions on the Sindri-owned server ('tmux -L sindri'), named
/// 'snd-&lt;launchId&gt;'. A red stub until U5 is green: every member throws.
/// </summary>
[SuppressMessage("Major Code Smell", "MA0182:Avoid unused internal types", Justification = "Registered in U7 (API P10a); a red stub until then.")]
internal sealed class TmuxSessionHost(TmuxHostOptions options) : ISessionHost
{
    public TmuxHostOptions Options { get; } = options;

    /// <inheritdoc/>
    public HostKind Kind => HostKind.Tmux;

    /// <inheritdoc/>
    public ValueTask<Result<HostRef>> Start(HostSession session, TimeSpan timeout, CancellationToken cancellationToken) =>
        throw new NotSupportedException("TmuxSessionHost: make SessionHostContract pass (U5)");

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> Stop(HostRef host, CancellationToken cancellationToken) =>
        throw new NotSupportedException("TmuxSessionHost: make SessionHostContract pass (U5)");

    /// <inheritdoc/>
    public ValueTask<int> AttachedClients(HostRef host, CancellationToken cancellationToken) =>
        throw new NotSupportedException("TmuxSessionHost: make SessionHostContract pass (U5)");

    /// <inheritdoc/>
    public IReadOnlyList<string> AttachArgv(HostRef host) =>
        throw new NotSupportedException("TmuxSessionHost: make SessionHostContract pass (U5)");

    /// <inheritdoc/>
    public ValueTask<HostFacts> Probe(CancellationToken cancellationToken) =>
        throw new NotSupportedException("TmuxSessionHost: make SessionHostContract pass (U5)");
}
