namespace Aw.Context.Core.Code;

/// <summary>The stem of a file name: the name without its last extension.</summary>
internal static class PathStem
{
    /// <summary>The file name without its last extension (a leading dot is part of the name).</summary>
    /// <param name="fileName">The last path segment.</param>
    /// <returns>The stem.</returns>
    internal static string Of(string fileName)
    {
        var dot = fileName.LastIndexOf('.');
        return dot > 0 ? fileName[..dot] : fileName;
    }
}
