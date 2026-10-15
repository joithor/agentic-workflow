namespace Aw.Meta.Host.Commands;

/// <summary>One command: its name, what it accepts, whether it needs the team config and its handler.</summary>
/// <param name="Name">The command name.</param>
/// <param name="Spec">The arguments it accepts.</param>
/// <param name="NeedsConfig">Whether it fails with AW-META-002 when there is no team config.</param>
/// <param name="Run">The handler: binds the context's arguments, calls one service and maps the result.</param>
internal sealed record CommandEntry(string Name, CommandSpec Spec, bool NeedsConfig, Func<CommandContext, Task<int>> Run);
