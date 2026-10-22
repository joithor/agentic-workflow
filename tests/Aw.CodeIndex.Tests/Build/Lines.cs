namespace Aw.CodeIndex.Tests.Build;

/// <summary>Collects progress lines synchronously, so a test reads them in order without a thread hop.</summary>
internal sealed class Lines : IProgress<string>
{
    private readonly List<string> _lines = [];

    public IReadOnlyList<string> All => _lines;

    public void Report(string value) => _lines.Add(value);
}
