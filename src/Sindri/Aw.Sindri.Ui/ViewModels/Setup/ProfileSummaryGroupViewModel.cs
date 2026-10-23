namespace Aw.Sindri.Ui.ViewModels.Setup;

/// <summary>One group of the approval's summary: a heading, a line under it for a repo's path and size, and the plain sentences, as the API words them.</summary>
internal sealed class ProfileSummaryGroupViewModel
{
    internal ProfileSummaryGroupViewModel(string heading, string detail, IReadOnlyList<string> lines)
    {
        (Heading, Detail, Lines) = (heading, detail, lines);
        Name = detail.Length > 0 ? heading + ", " + detail : heading;
    }

    /// <summary>The group's heading, or a repo's name.</summary>
    public string Heading { get; }

    /// <summary>A repo's path, file count and size; empty for the other groups.</summary>
    public string Detail { get; }

    /// <summary>True when there is a path and size line.</summary>
    public bool HasDetail => Detail.Length > 0;

    /// <summary>The sentences.</summary>
    public IReadOnlyList<string> Lines { get; }

    /// <summary>The group's accessible name.</summary>
    public string Name { get; }
}
