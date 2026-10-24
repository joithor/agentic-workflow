using System.Text.Json.Serialization;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>Claude Code's 'hooks' settings, as far as a Step's session uses them: the turn's end and a new prompt.</summary>
/// <param name="Stop">Runs when the agent's turn ends.</param>
/// <param name="UserPromptSubmit">Runs when a prompt starts a turn.</param>
internal sealed record ClaudeHooks(
    [property: JsonPropertyName("Stop")] IReadOnlyList<ClaudeHookGroup> Stop,
    [property: JsonPropertyName("UserPromptSubmit")] IReadOnlyList<ClaudeHookGroup> UserPromptSubmit);

/// <summary>One matcher-less group of hook commands.</summary>
/// <param name="Hooks">The commands.</param>
internal sealed record ClaudeHookGroup(IReadOnlyList<ClaudeHookCommand> Hooks);

/// <summary>One shell command hook.</summary>
/// <param name="Type">Always 'command'.</param>
/// <param name="Command">The shell text.</param>
internal sealed record ClaudeHookCommand(string Type, string Command);
