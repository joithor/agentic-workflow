namespace Aw.Context.Core.Trust;

/// <summary>
/// The one ingest boundary every record field crosses (spec §7): sanitize (HTML comments, invisible and control
/// characters, encoded blobs, remote images and link targets, until stable), then scrub. Titles and authors are
/// collapsed to one line.
/// </summary>
public interface IIngestFilter
{
    /// <summary>Cleans one field.</summary>
    /// <param name="raw">The field as the source returned it.</param>
    /// <param name="singleLine">True for titles and authors: whitespace runs collapse to one space.</param>
    /// <returns>The clean text, or a blocked field with no text.</returns>
    public ScrubbedField Clean(string raw, bool singleLine);
}
