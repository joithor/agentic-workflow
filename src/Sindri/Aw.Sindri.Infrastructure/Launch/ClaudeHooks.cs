using System.Text.Json.Serialization;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>Claude Code's 'hooks' settings, as far as a Step's session uses them: the turn's end and a new prompt.</summary>
/// <param name="Stop">Runs when the agent's turn ends.</param>
/// <param name="UserPromptSubmit">Runs when a prompt starts a turn.</param>
/// <param name="PreToolUse">Runs before each tool call: an agent woken without a prompt (a background task's notice) is working again.</param>
internal sealed record ClaudeHooks(
    [property: JsonPropertyName("Stop")] IReadOnlyList<ClaudeHookGroup> Stop,
    [property: JsonPropertyName("UserPromptSubmit")] IReadOnlyList<ClaudeHookGroup> UserPromptSubmit,
    [property: JsonPropertyName("PreToolUse")] IReadOnlyList<ClaudeHookGroup> PreToolUse);
