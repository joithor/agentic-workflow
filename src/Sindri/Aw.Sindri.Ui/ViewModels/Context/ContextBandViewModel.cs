namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>One band of the lens stack: a header with its count and the lines under it.</summary>
/// <param name="Header">The name and the count.</param>
/// <param name="Lines">The band's rows.</param>
internal sealed record ContextBandViewModel(string Header, IReadOnlyList<ContextLine> Lines);
