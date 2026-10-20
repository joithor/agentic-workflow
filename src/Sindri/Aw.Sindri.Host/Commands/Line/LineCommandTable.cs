namespace Aw.Sindri.Host.Commands.Line;

/// <summary>
/// The line's local verbs (line spec Appendix D, LN-API4): 'design check', 'plan check' and 'line desk'. With --facts they run in this process over a directory of packet
/// files (class local: no instance, no server); with an address they are clients of the control plane, and a serve that is down is SND-API-001, never started.
/// Exit 0 pass, 1 findings, 2 usage, a tool error or serve not running.
/// </summary>
internal static class LineCommandTable
{
    internal static Task<int> Run(string[] args, ScopingCommandTable.Host host, CancellationToken ct) => throw new NotSupportedException();
}
