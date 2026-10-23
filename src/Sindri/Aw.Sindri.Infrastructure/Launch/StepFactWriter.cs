using Aw.Kernel;
using Aw.Sindri.Core;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>Writes a Step's fact files into its run directory.</summary>
internal static class StepFactWriter
{
    /// <summary>Writes each file under <paramref name="runDirectory"/>, replacing what a retry left there.</summary>
    /// <param name="runDirectory">The run directory.</param>
    /// <param name="files">File name to content.</param>
    /// <returns>An error when a name would leave the directory (nothing is written then); null when every file is written.</returns>
    internal static Error? Write(string runDirectory, IReadOnlyDictionary<string, string> files)
    {
        var root = Path.GetFullPath(runDirectory);
        foreach (var name in files.Keys)
        {
            if (!string.Equals(Path.GetFileName(name), name, StringComparison.Ordinal) || name is "" or "." or "..")
            {
                return SindriErrors.RequestInvalid.With($"the fact file '{name}' is not one file in the run directory, so no fact files were written");
            }
        }

        _ = Directory.CreateDirectory(root);
        foreach (var (name, content) in files)
        {
            File.WriteAllText(Path.Combine(root, name), content);
        }

        return null;
    }
}
