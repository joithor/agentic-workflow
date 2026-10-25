namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>One matcher-less group of hook commands.</summary>
/// <param name="Hooks">The commands.</param>
internal sealed record ClaudeHookGroup(IReadOnlyList<ClaudeHookCommand> Hooks);
