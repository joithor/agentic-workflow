using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// Import a map file as the current map (context spec §8.7, a protected command shaped like 'profile approve'). Without a
/// hash it only checks the file and shows its content hash. With one, <see cref="Confirmed"/> must be set: the CLI sets it
/// only after the human typed the hash's first six characters at an interactive terminal. The API lane's shared challenge
/// is not on this base yet; the import adopts it when that lane ships it.
/// </summary>
/// <param name="Path">The map file's absolute path, outside every git worktree.</param>
/// <param name="Hash">The content hash to import, or null to see it.</param>
/// <param name="Confirmed">True only after the human's typed confirmation.</param>
public sealed record ImportContextMapRequest(string Path, string? Hash, bool Confirmed) : ICommand<ImportContextMapResponse>;
