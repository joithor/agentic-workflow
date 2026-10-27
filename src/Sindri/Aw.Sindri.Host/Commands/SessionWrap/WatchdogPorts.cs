using Aw.Sindri.Core.Sandbox;

namespace Aw.Sindri.Host.Commands.SessionWrap;

/// <summary>What 'session wrap --limits' watches the scope through.</summary>
/// <param name="Scope">systemd's view of the scope.</param>
/// <param name="Stats">The scope's accounting files.</param>
internal sealed record WatchdogPorts(IScopeControl Scope, ICgroupStats Stats);
