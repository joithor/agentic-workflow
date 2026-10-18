using System.Text;
using Aw.Kernel;
using Aw.Sindri.Core.Scoping;

namespace Aw.Sindri.Host.Commands.Scoping;

/// <summary>
/// A directory of fact files (scoping spec §8.2, W11's Step directory): '&lt;key&gt;.json' holds fact:&lt;key&gt;'s JSON, and '&lt;key&gt;.jsonl'
/// holds a list one value per line (rounds.jsonl is fact:rounds). A draft directory overlays the files it names.
/// </summary>
internal static class ScopingFactDirectory
{
    /// <summary>Reads the facts under <paramref name="directory"/>; names the directory in the error when it is missing.</summary>
    internal static async Task<Result<Dictionary<string, string>>> Read(string directory, CancellationToken ct)
    {
        if (!Directory.Exists(directory))
        {
            return Missing(directory);
        }

        var facts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(directory).Order(StringComparer.Ordinal))
        {
            var extension = Path.GetExtension(file);
            var key = Path.GetFileNameWithoutExtension(file);
            if (string.Equals(extension, ".json", StringComparison.Ordinal))
            {
                facts[key] = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
            }
            else if (string.Equals(extension, ".jsonl", StringComparison.Ordinal))
            {
                var lines = (await File.ReadAllLinesAsync(file, ct).ConfigureAwait(false)).Where(static l => l.Length > 0);
                facts[key] = "[" + string.Join(',', lines) + "]";
            }
        }

        return facts;
    }

    /// <summary>
    /// Takes fact:rounds from the out directory's rounds.jsonl when it has one. The desk appends each round there (the run area, where the I/O Step
    /// keeps it), so the next round's number, trigger and diff come from the rounds it already wrote, not from the facts directory's copy.
    /// </summary>
    internal static async Task OverlayRounds(Dictionary<string, string> facts, string outDirectory, CancellationToken ct)
    {
        var path = Path.Combine(outDirectory, "rounds.jsonl");
        if (File.Exists(path))
        {
            var lines = (await File.ReadAllLinesAsync(path, ct).ConfigureAwait(false)).Where(static l => l.Length > 0);
            facts["rounds"] = "[" + string.Join(',', lines) + "]";
        }
    }

    /// <summary>Whether the path sits inside a git working tree: it or an ancestor holds a '.git' entry (a directory, or the file a linked worktree has).</summary>
    internal static bool InGitWorktree(string path)
    {
        for (var directory = new DirectoryInfo(Path.GetFullPath(path)); directory is not null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git")) || File.Exists(Path.Combine(directory.FullName, ".git")))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The refusal for a draft or output directory inside a git worktree (SND-SCOPING-002).</summary>
    internal static Error WorktreeRefusal(string flag, string path) =>
        ScopingErrors.DraftOutsideRunArea.With($"{flag} {path} is inside a git worktree (fix: write it under the instance's run area, $AW_SINDRI_HOME/workflow/runs/<instance>/, or any directory outside a checkout)");

    private static Error Missing(string directory) =>
        Core.SindriErrors.InvalidInput.With($"{directory} is not a directory (fix: pass the directory of fact files, for example --facts \"$AW_SINDRI_RUN_DIR\")");

    /// <summary>Writes text to a file, creating its directory; the content as UTF-8 without a byte order mark.</summary>
    internal static async Task Write(string path, string content, bool append, CancellationToken ct)
    {
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        if (append)
        {
            await File.AppendAllTextAsync(path, content, encoding, ct).ConfigureAwait(false);
        }
        else
        {
            await File.WriteAllTextAsync(path, content, encoding, ct).ConfigureAwait(false);
        }
    }
}
