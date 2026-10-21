namespace Aw.Sindri.Ui.Views.Controls;

/// <summary>A typed confirmation pressed on its exact phrase.</summary>
internal sealed class SubmittedEventArgs(string text) : EventArgs
{
    /// <summary>The typed text, trimmed.</summary>
    internal string Text { get; } = text;
}
