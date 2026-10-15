using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Testing;

/// <summary>A scrubber that returns its text unchanged: for tests whose text holds no secret.</summary>
public sealed class PassThroughScrubber : ITextScrubber
{
    /// <inheritdoc/>
    public string Scrub(string text) => text;
}
