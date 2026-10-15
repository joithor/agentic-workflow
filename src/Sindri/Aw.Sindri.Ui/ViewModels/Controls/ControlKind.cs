namespace Aw.Sindri.Ui.ViewModels.Controls;

/// <summary>How a catalog row's control is drawn (UI spec U3, §7), from the row's UI status as the API serves it.</summary>
internal enum ControlKind
{
    /// <summary>'ready': a button.</summary>
    Ready,

    /// <summary>'pending:&lt;phase&gt;': a disabled button, with its phase and CLI on a focusable line.</summary>
    Pending,

    /// <summary>'cli-only:&lt;reason&gt;': a copy-the-CLI block; the app never runs it.</summary>
    CliOnly,
}
