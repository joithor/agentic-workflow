using Aw.Evidence.Core.Scripts;

namespace Aw.Evidence.Features.RunScript;

/// <summary>A script as read: the parsed document and the SHA-256 of the file's bytes.</summary>
/// <param name="Script">The script.</param>
/// <param name="Sha256">The hex digest of the file as read.</param>
internal sealed record LoadedScript(UiScript Script, string Sha256);
