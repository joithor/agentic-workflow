namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>A kind's look: the chip's shape, its mark, and the theme brush of the mark (a token key, never a colour).</summary>
/// <param name="Shape">The chip shape.</param>
/// <param name="Mark">The mark.</param>
/// <param name="Accent">The theme token of the mark.</param>
internal sealed record NodeKindStyle(ChipShape Shape, KindMark Mark, string Accent);
