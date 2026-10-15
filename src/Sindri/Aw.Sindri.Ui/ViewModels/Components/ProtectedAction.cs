namespace Aw.Sindri.Ui.ViewModels.Components;

#pragma warning disable MA0182 // until the U8 pages place the component
/// <summary>What the lock button needs from a catalog row: the verb, the served CLI line, and what it acts on.</summary>
/// <param name="Verb">The action's verb as the label shows it ("Accept", "Grant rounds").</param>
/// <param name="Cli">The row's served `sindri …` line.</param>
/// <param name="Subject">What it acts on (an address or a title); it goes in the accessible names.</param>
internal sealed record ProtectedAction(string Verb, string Cli, string Subject);
#pragma warning restore MA0182
