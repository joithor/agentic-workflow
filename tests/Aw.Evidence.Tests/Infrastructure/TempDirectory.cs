using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Infrastructure;

/// <summary>A fresh directory under the system temp path, deleted on dispose.</summary>
public sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        // Short and under /tmp-like roots: Chromium's sockets and macOS path limits dislike deep temp paths.
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "aw-ev-" + RealTime.NewGuid().ToString("N")[..12]);
        _ = Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string Combine(string relative) => System.IO.Path.Combine(Path, relative);

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
