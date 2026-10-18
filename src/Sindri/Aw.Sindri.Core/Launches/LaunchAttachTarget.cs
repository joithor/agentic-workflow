using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Launches;

/// <summary>A live session to attach to.</summary>
/// <param name="Target">The agent running in it.</param>
/// <param name="TabTitle">The viewer's tab title, or null.</param>
/// <param name="Since">When it started.</param>
/// <param name="Argv">The attach command.</param>
public sealed record LaunchAttachTarget(AgentId Target, string? TabTitle, DateTimeOffset Since, IReadOnlyList<string>? Argv);
