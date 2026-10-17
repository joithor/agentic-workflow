using Aw.Kernel.Testing;

namespace Aw.Context.Tests.Map;

/// <summary>A fresh temp folder, deleted on dispose: Sindri's app home for one test.</summary>
internal sealed class TempHome : IDisposable
{
    public TempHome()
    {
        _ = Directory.CreateDirectory(Root);
    }

    public string Root { get; } = Path.Combine(Path.GetTempPath(), "aw-context-" + RealTime.NewGuid().ToString("N")[..12]);

    public Func<string, string?> Environment => name => name == "AW_SINDRI_HOME" ? Root : null;

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
