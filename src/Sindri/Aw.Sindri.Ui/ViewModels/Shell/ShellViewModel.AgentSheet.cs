namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The session sheet in the shell (<see cref="ShellViewModel"/>, continued; U11a T3, T4).</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>The session whose Agents row opened the sheet (its ref), where Esc returns focus; null when it was opened some other way.</summary>
    internal string? OpenerSession { get; private set; }
}
