using Aw.Kernel;
using Aw.Sindri.Core.LensPacks;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>Writes a Step's pack bundle into its directory (LN-WF5).</summary>
internal static class PackBundleFiles
{
    /// <summary>Writes the bundle's files under '&lt;stepDirectory&gt;/&lt;bundle.Directory&gt;/', replacing what a retry left there.</summary>
    /// <param name="stepDirectory">The Step directory.</param>
    /// <param name="bundle">The bundle.</param>
    /// <returns>An error when a file's path would leave the bundle's folder (nothing is written then); null when every file is written.</returns>
    internal static Error? Write(string stepDirectory, PackBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        var root = Path.GetFullPath(Path.Combine(stepDirectory, bundle.Directory));
        var targets = new List<(string Path, string Text)>();
        foreach (var (relative, text) in bundle.Files)
        {
            var target = Path.GetFullPath(Path.Combine(root, relative));
            if (Path.IsPathRooted(relative) || !target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                return PackErrors.Invalid.With($"the pack '{bundle.PackId.Value}' has a file '{relative}' outside its folder, so its bundle was not written");
            }

            targets.Add((target, text));
        }

        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }

        foreach (var (path, text) in targets)
        {
            _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);   // why: a file's path always has a directory
            File.WriteAllText(path, text);
        }

        return null;
    }
}
