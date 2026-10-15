using System.ComponentModel;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Palette;

/// <summary>The command palette (<see cref="CommandPaletteViewModel"/>, continued): the runs it started and how it announces them.</summary>
internal sealed partial class CommandPaletteViewModel
{
    // The entries the palette ran whose control it mutes until it has announced the outcome (true: a copy).
    private readonly Dictionary<PaletteEntry, bool> _ran = [];

    // The palette speaks the lifecycle of the entries it runs through its own live region, so it mutes those entries' controls
    // for that run and no others (a run that did not start here is spoken by its control, whatever is open): the run is
    // announced once. The mute ends when the run's outcome is announced here, or when the palette closes first.
    private void Track(PaletteEntry entry, ControlViewModel control, bool copy)
    {
        Release(entry);
        _ran[entry] = copy;
        control.IsMuted = true;
        entry.PropertyChanged += OnRanChanged;
    }

    private void Release(PaletteEntry entry)
    {
        if (_ran.Remove(entry))
        {
            entry.PropertyChanged -= OnRanChanged;
            entry.Control?.IsMuted = false;
        }
    }

    private void ReleaseAll() => _ran.Keys.ToList().ForEach(Release);

    private async Task Copy(PaletteEntry entry, ControlViewModel control, CancellationToken cancellationToken)
    {
        Track(entry, control, copy: true);
        await control.Copy(cancellationToken).ConfigureAwait(true);
        if (!IsCopied(control))
        {
            Release(entry);   // nothing was copied: nothing to announce, nothing to wait for
        }
    }

    private async Task Send(PaletteEntry entry, ControlViewModel control, CancellationToken cancellationToken)
    {
        if (RunPolicy == PaletteRunPolicy.CloseFirst)
        {
            Close();
        }
        else
        {
            Track(entry, control, copy: false);
        }

        try
        {
            await control.Run(cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            if (!control.IsSending)
            {
                Release(entry);   // normally released at the outcome already; this covers a cancelled send
            }
        }
    }

    private static bool IsCopied(ControlViewModel control) => string.Equals(control.CopyLabel, Labels.Copied, StringComparison.Ordinal);

    // A run of the palette's own changed. Its outcome, "Sending…" (as the control would say it, after 300 ms) and "Copied" are
    // announced: as the selected entry with its position, or by its name alone when the selection or the query moved on. What
    // is not announced: the first 300 ms of a send (the region would say the entry again) and the settling back to ready.
    private void OnRanChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PaletteEntry.Name) || sender is not PaletteEntry { Control: { } control } entry || !_ran.TryGetValue(entry, out var copy))
        {
            return;
        }

        if (Speaks(control))
        {
            Say(entry);
        }

        if (copy ? !IsCopied(control) : control.IsAccepted || control.IsFailed)
        {
            Release(entry);
        }
    }

    private static bool Speaks(ControlViewModel control) => control.IsAnnouncingSending || control.IsAccepted || control.IsFailed || IsCopied(control);

    private void Say(PaletteEntry entry)
    {
        if (entry == Selected)
        {
            Announce();
        }
        else
        {
            Announcement = entry.Name;
        }
    }
}
