namespace Aw.Context.Core.Extract;

/// <summary>The forms the extractor recognizes (S-4), each pure over one file's text. Only source files count: not tests, not vendored or built output.</summary>
public static class Recognizers
{
    /// <summary>Every recognizer, in the order their findings are reported.</summary>
    public static IReadOnlyList<Recognizer> All { get; } = [];

    /// <summary>Whether the extractor reads a file at all.</summary>
    /// <param name="file">The file.</param>
    /// <returns>True for a small source file outside tests and build output.</returns>
    public static bool Source(RepoFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        throw new NotSupportedException("S-4: Recognizers.Source is not built yet");
    }
}
