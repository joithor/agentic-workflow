namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>One step of the Files breadcrumb: "~" for the top of the repo, then a folder each. Every step but the last is a link.</summary>
/// <param name="Text">What the step says: "~" or the folder's name.</param>
/// <param name="Path">The folder it goes to (as served, ending in a slash), or null for the top.</param>
/// <param name="IsLink">Whether the step goes anywhere; the last step is the folder shown, plain text.</param>
internal sealed record FileCrumb(string Text, string? Path, bool IsLink)
{
    /// <summary>Whether the step is the folder shown, plain text.</summary>
    public bool IsPlain => !IsLink;

    /// <summary>Whether a slash comes before the step: every step but "~".</summary>
    public bool HasSeparator => Path is not null;
}
