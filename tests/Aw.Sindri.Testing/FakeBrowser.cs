using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Testing;

/// <summary>A browser that records the pages it was asked to open.</summary>
public sealed class FakeBrowser : IBrowser
{
    /// <summary>Gets the pages opened, in order.</summary>
    public IList<Uri> Opened { get; } = [];

    /// <summary>Gets or sets a value indicating whether opening succeeds.</summary>
    public bool Works { get; set; } = true;

    /// <inheritdoc />
    public bool Open(Uri url)
    {
        Opened.Add(url);
        return Works;
    }
}
