using Aw.Kernel.Testing;

namespace Aw.Packets.Tests.Contracts;

/// <summary>A fresh directory under the system temp path, deleted on dispose.</summary>
public sealed class TempDirectory : IDisposable
{
    /// <summary>Creates the directory.</summary>
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "aw-packets-test-" + RealTime.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(Path);
    }

    /// <summary>The directory.</summary>
    public string Path { get; }

    /// <summary>Writes <paramref name="files"/> under <paramref name="relative"/>.</summary>
    /// <param name="relative">A sub-directory ('' for the root).</param>
    /// <param name="files">Files by relative path.</param>
    /// <returns>The sub-directory's full path.</returns>
    public string Write(string relative, IReadOnlyDictionary<string, byte[]> files)
    {
        var root = System.IO.Path.Combine(Path, relative);
        foreach (var (name, bytes) in files)
        {
            var full = System.IO.Path.Combine(root, name);
            _ = Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
            File.WriteAllBytes(full, bytes);
        }

        return root;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
