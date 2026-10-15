namespace Aw.Sindri.Ui.ViewModels.Palette;

/// <summary>What the palette does when Enter sends a ready command (U1 finding F2; the owner decides, UI spec §4.1, §8).</summary>
internal enum PaletteRunPolicy
{
    /// <summary>The provisional default: the palette stays open and the entry shows the §4.1 lifecycle (sending, done, or Sindri's code and detail).</summary>
    StayOpen,

    /// <summary>The palette closes, then sends: the outcome shows only where the control is drawn elsewhere.</summary>
    CloseFirst,
}
