using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>A scrubber that swaps one word, so a test can see that a title went through it.</summary>
internal sealed class ReplacingScrubber(string word, string with) : ITextScrubber
{
    public string Scrub(string text) => text.Replace(word, with, StringComparison.Ordinal);
}
