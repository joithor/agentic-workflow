namespace Aw.Judge.Core.Questions;

public static partial class UntrustedText
{
    /// <summary>
    /// A set of files cut to <paramref name="cap"/> characters of text by whole files, in the order <see cref="CappedDiff"/> uses
    /// (files under src/ first, then the rest, each in the order given): a file is kept only if it still fits, and when none fits
    /// the first in that order is cut at a line instead of dropped. Whatever was dropped or cut is named with its size.
    /// </summary>
    /// <param name="files">The files, with their text.</param>
    /// <param name="cap">How many characters of text to keep.</param>
    /// <returns>The kept files (in the order given), the dropped ones and the cut one.</returns>
    public static CappedFileSet CappedFiles(IReadOnlyList<ModelPickFile> files, int cap) => throw new NotSupportedException("model-pick is not built yet");
}
