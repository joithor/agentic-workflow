using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Queue;

/// <summary>Red stubs for §8's hold, key guard, filter and Y.</summary>
internal sealed partial class QueueViewModel
{
    internal static readonly TimeSpan KeyGuard = TimeSpan.FromMilliseconds(500);

    public bool HasHeld { get; }

    public string HeldText { get; } = string.Empty;

    public bool IsGuarded { get; }

    public bool IsFiltering { get; private set; }

    public string FilterText { get; set; } = string.Empty;

    internal void Hold(bool holding) => IsFiltering = holding && IsFiltering;

    internal void ShowHeld() => IsFiltering = IsFiltering && HasHeld;

    internal void ClearFilter() => IsFiltering = IsGuarded;

    internal ControlViewModel? SelectedCopy() => Groups.Count > 99 ? Groups[0].Rows[0].PrimaryAction : null;
}
