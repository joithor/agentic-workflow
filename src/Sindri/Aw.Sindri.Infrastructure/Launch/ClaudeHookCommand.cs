namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>One shell command hook.</summary>
/// <param name="Type">Always 'command'.</param>
/// <param name="Command">The shell text.</param>
internal sealed record ClaudeHookCommand(string Type, string Command);
