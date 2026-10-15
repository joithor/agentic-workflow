using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// Import a map file as the current map (context spec §8.7), a protected command (engine spec §4.2, README X30). Without a
/// hash it checks the file, shows its content hash and issues the server's challenge 'import map &lt;hash6&gt;' bound to
/// that hash. With one, <see cref="Answer"/> must redeem that challenge: the CLI sends it only after the human typed the
/// action text at an interactive terminal.
/// </summary>
/// <param name="Path">The map file's absolute path, outside every git worktree.</param>
/// <param name="Hash">The content hash to import, or null to see it and get a challenge.</param>
/// <param name="Answer">The challenge and the text the human typed, or null.</param>
public sealed record ImportContextMapRequest(string Path, string? Hash = null, ChallengeAnswer? Answer = null) : ICommand<ImportContextMapResponse>;
