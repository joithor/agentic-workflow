using System.Globalization;
using Aw.CodeIndex.Core.Ports;

namespace Aw.CodeIndex.Infrastructure.Embedding;

/// <summary>
/// Reads context/code-index.lock from AW_CODE_INDEX_LOCK, else from the nearest 'context/code-index.lock' above the running program, and compares its 'ollama-model' row with the pin the embedder carries.
/// The row shapes are those of scripts/code-index-lock.sh.
/// </summary>
/// <param name="environment">Reads an environment variable.</param>
internal sealed class FileCodeIndexPins(Func<string, string?> environment) : ICodeIndexPins
{
    public LockState Check()
    {
        var path = Locate();
        if (path is null)
        {
            return LockState.Missing;
        }

        string[] lines;
        try
        {
            lines = File.ReadAllLines(path);
        }
        catch (IOException)
        {
            return LockState.Missing;
        }
        catch (UnauthorizedAccessException)
        {
            return LockState.Missing;
        }

        return Compare(lines);
    }

    internal static LockState Compare(IEnumerable<string> lines)
    {
        var rows = lines.Select(static l => l.Trim()).Where(static l => l.Length > 0 && l[0] != '#').Select(static l => l.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToList();
        var graphify = rows.Find(static r => r[0] == "graphify");
        var model = rows.Find(static r => r[0] == "ollama-model");
        var need = rows.Find(static r => r[0] == "need");
        if (rows.Exists(static r => r[0] is not ("graphify" or "ollama-model" or "need")) || graphify is not { Length: 4 } || model is not { Length: 3 } || !DiskRow(need))
        {
            return LockState.Unparsable;
        }

        var pin = OllamaEmbedder.DefaultPin;
        return model[1] == pin.Name && model[2] == pin.Digest ? LockState.Matches : LockState.Mismatch;
    }

    private static bool DiskRow(string[]? need) =>
        need is ["need", "disk-bytes", var bytes] && long.TryParse(bytes, NumberStyles.None, CultureInfo.InvariantCulture, out _);

    private string? Locate()
    {
        if (environment("AW_CODE_INDEX_LOCK") is { Length: > 0 } given)
        {
            return File.Exists(given) ? given : null;
        }

        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "context", "code-index.lock");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
