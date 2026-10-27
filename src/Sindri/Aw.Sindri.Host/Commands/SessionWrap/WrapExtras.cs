namespace Aw.Sindri.Host.Commands.SessionWrap;

/// <summary>What 'session wrap' can be given beyond its calls, launcher and clock; each is optional.</summary>
/// <param name="Hangup">Completes when wrap is told to hang up or terminate (its terminal closed); null when nothing can tell it.</param>
/// <param name="Watchdog">What '--limits' watches the scope through; null where nothing can (then '--limits' refuses the start).</param>
internal sealed record WrapExtras(Task? Hangup = null, WatchdogPorts? Watchdog = null);
