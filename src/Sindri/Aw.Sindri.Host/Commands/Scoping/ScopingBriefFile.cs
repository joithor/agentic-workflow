using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Scoping;

namespace Aw.Sindri.Host.Commands.Scoping;

/// <summary>
/// The brief file 'read-brief' reads at the desk (scoping spec §8.2, SND-SCOPING-004): a path inside the repo root or the notes dir, never under a
/// denied glob (an environment file, a key, a credentials file, .git, .ssh), and never through a symbolic link that leaves the root. The result is the
/// 'briefSource' fact the block turns into fact:brief.
/// </summary>
internal static class ScopingBriefFile
{
    private const long MaxBytes = 1024 * 1024;

    private static readonly string[] _deniedSegments = [".git", ".ssh", ".aws", ".gnupg", ".env", ".netrc", ".npmrc"];
    private static readonly string[] _deniedExtensions = [".pem", ".key", ".p12", ".pfx", ".keystore", ".secret", ".secrets"];
    private static readonly string[] _deniedPrefixes = [".env.", "id_rsa", "id_ed25519", "id_ecdsa", "credentials", "secrets."];

    /// <summary>Reads the file the scopeInput names; the result is the briefSource fact's JSON text.</summary>
    internal static async Task<Result<string>> Source(string scopeInputJson, IReadOnlyList<string> roots, DateTimeOffset now, CancellationToken ct)
    {
        var path = Path(scopeInputJson);
        if (path is null)
        {
            return SindriErrors.InvalidInput.With("fact:scopeInput has no path (fix: write scopeInput.json as {\"kind\":\"brief\",\"repo\":\"...\",\"path\":\"...\"})");
        }

        if (roots.Count == 0)
        {
            return SindriErrors.InvalidInput.With("read-brief needs --repo-root <dir> (or --notes-dir <dir>): the directory the brief's path is inside (fix: pass the repo's checkout)");
        }

        var resolved = Resolve(path, roots);
        if (resolved is null)
        {
            return Refused(path, "it is outside the repo root and the notes dir, or under a denied glob");
        }

        if (!File.Exists(resolved))
        {
            return SindriErrors.InvalidInput.With($"the brief {path} does not exist under {string.Join(" or ", roots)} (fix: name a file that exists)");
        }

        if (new FileInfo(resolved).Length > MaxBytes)
        {
            return SindriErrors.InvalidInput.With($"the brief {path} is over {MaxBytes.ToString(CultureInfo.InvariantCulture)} bytes (fix: name one section with \"section\", or a smaller file)");
        }

        var text = await File.ReadAllTextAsync(resolved, ct).ConfigureAwait(false);
        var version = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..12];
        return new JsonObject { ["path"] = path, ["text"] = text, ["version"] = version, ["fetchedAt"] = now.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture) }.ToJsonString();
    }

    private static Error Refused(string path, string why) =>
        ScopingErrors.BriefPathRefused.With($"the brief path {path} is refused: {why} (fix: name a file inside the repo root or the notes dir that is not a secret)");

    private static string? Path(string scopeInputJson)
    {
        try
        {
            return JsonNode.Parse(scopeInputJson) is JsonObject obj && obj["path"] is JsonValue value && value.TryGetValue<string>(out var path) && path.Length > 0 ? path : null;
        }
        catch (JsonException)
        {
            return null;   // boundary: the fact is text a person wrote
        }
    }

    /// <summary>The file's full path when its real location (every directory and the file itself followed through links) is inside the root's real location and not denied; null otherwise.</summary>
    private static string? Resolve(string path, IReadOnlyList<string> roots)
    {
        foreach (var root in roots.Select(static r => System.IO.Path.GetFullPath(r)))
        {
            var candidate = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, path));
            var real = Real(candidate);
            if (Inside(root, candidate) && Inside(Real(root), real) && !Denied(System.IO.Path.GetRelativePath(root, candidate)) && !Denied(System.IO.Path.GetRelativePath(Real(root), real)))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>The path with every existing component followed through its link, so a symlinked directory counts as where it points.</summary>
    private static string Real(string path)
    {
        var full = System.IO.Path.GetFullPath(path);
        var current = System.IO.Path.GetPathRoot(full) ?? string.Empty;
        foreach (var part in full[current.Length..].Split(System.IO.Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = System.IO.Path.Combine(current, part);
            if (new FileInfo(current).ResolveLinkTarget(returnFinalTarget: true) is { } target)
            {
                current = Real(target.FullName);
            }
        }

        return current;
    }

    private static bool Inside(string root, string path) => path.StartsWith(root.TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar, StringComparison.Ordinal);

    private static bool Denied(string relative) =>
        relative.Split('/', '\\').Any(static segment =>
            _deniedSegments.Contains(segment, StringComparer.OrdinalIgnoreCase)
            || _deniedExtensions.Contains(System.IO.Path.GetExtension(segment), StringComparer.OrdinalIgnoreCase)
            || _deniedPrefixes.Any(prefix => segment.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)));
}
