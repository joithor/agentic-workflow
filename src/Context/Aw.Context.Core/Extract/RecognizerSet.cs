namespace Aw.Context.Core.Extract;

/// <summary>The profile's recognizers as the extractor and the as-of map cache see them (S-SK): which extra paths to read, and a hash that keys a cached extraction.</summary>
public sealed class RecognizerSet
{
    private RecognizerSet()
    {
    }

    /// <summary>No profile recognizers: the extractor's built-in table alone.</summary>
    public static RecognizerSet None { get; } = new();

    /// <summary>'' for none, else 'r' and 8 hex digits of the recognizers' content, so a changed set never reuses a cached extraction.</summary>
    public string Hash { get; } = "";

    /// <summary>The set of these recognizers.</summary>
    /// <param name="recognizers">The checked entries.</param>
    /// <returns>The set.</returns>
    public static RecognizerSet From(IReadOnlyList<ProfileRecognizer> recognizers) => throw new NotSupportedException();

    /// <summary>Whether a recognizer's glob matches the path, so the file is read even though the built-in table skips it. Deny globs are checked by the caller first.</summary>
    /// <param name="path">The repo-relative path.</param>
    /// <returns>True when a recognizer reads the path.</returns>
    public bool Wants(string path) => throw new NotSupportedException();
}
