using Aw.Sindri.Ui.ViewModels.Palette;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The shell's overlays (UI spec §8): the command palette and the shortcuts sheet, built in the constructor.</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>The command palette (§8: ⌘K).</summary>
    public CommandPaletteViewModel Palette { get; }

    /// <summary>The shortcuts sheet (§8: ?).</summary>
    public ShortcutsViewModel Shortcuts { get; }
}
