using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Palette;

/// <summary>
/// The shell's polite live region for a command's lifecycle when no control of its own can speak it (UI spec §5, "every outcome
/// announced once through the polite region"): a command the palette started whose palette closed (Esc, or the close-first
/// policy) before the outcome, and whose control has no visible view. Always in the tree; the flag changes before the text and
/// the text is never empty.
/// </summary>
/// <param name="viewSpeaks">True when the control's own view is on show, so the control's live region speaks its outcome itself.</param>
internal sealed class CommandOutcomeRegion(Func<ControlViewModel, bool> viewSpeaks) : ObservableObject
{
    /// <summary>The region's gate.</summary>
    public bool IsLive { get; private set => Set(ref field, value); }

    /// <summary>The region's text, never empty.</summary>
    public string Text { get; private set => Set(ref field, value); } = Labels.Sindri;

    /// <summary>True when the control's own view is on show.</summary>
    /// <param name="control">The control.</param>
    /// <returns>True when it speaks itself.</returns>
    internal bool ViewSpeaks(ControlViewModel control) => viewSpeaks(control);

    /// <summary>Says a text.</summary>
    /// <param name="text">The text.</param>
    internal void Say(string text)
    {
        if (IsLive && string.Equals(Text, text, StringComparison.Ordinal))
        {
            IsLive = false;   // the same text again is still a change: dark first, then the resting text
        }

        if (!IsLive)
        {
            Text = Labels.Sindri;
            IsLive = true;
        }

        Text = text;
    }
}
