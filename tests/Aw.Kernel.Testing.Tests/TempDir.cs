namespace Aw.Kernel.Testing.Tests;

/// <summary>A temp directory deleted afterwards, for tests that read report files.</summary>
public sealed class TempDir : IDisposable
{
    public string Path { get; } = Directory.CreateTempSubdirectory("aw-lane-cap-").FullName;

    public string Write(string name, string text)
    {
        var file = System.IO.Path.Combine(Path, name);
        File.WriteAllText(file, text);
        return file;
    }

    public void Dispose() => Directory.Delete(Path, recursive: true);
}
