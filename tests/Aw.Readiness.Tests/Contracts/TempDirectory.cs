namespace Aw.Readiness.Tests.Contracts;

/// <summary>A temp directory deleted on dispose, for the integration-lane adapter tests.</summary>
internal sealed class TempDirectory : IDisposable
{
    public string Path { get; } = Directory.CreateTempSubdirectory("aw-readiness-test-").FullName;

    public void Dispose() => Directory.Delete(Path, recursive: true);
}
