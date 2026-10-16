namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>One tab of the frame's strip: its label, the served count (none hides the number), and the phase that makes it live.</summary>
/// <param name="label">The tab's word.</param>
/// <param name="count">The served count; null when the API serves none.</param>
/// <param name="phase">The phase that brings the tab's content.</param>
internal sealed class FrameTabViewModel(string label, int? count, string phase)
{
    /// <summary>The tab's word.</summary>
    public string Label { get; } = label;

    /// <summary>The label and, when served, the count, as the strip draws them.</summary>
    public string Text { get; } = Labels.TabText(label, count);

    /// <summary>The phase note ("arrives with U8b").</summary>
    public string Phase { get; } = phase;

    /// <summary>The accessible name: the text and the phase.</summary>
    public string Name { get; } = Labels.TabName(Labels.TabText(label, count), phase);
}
