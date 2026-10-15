namespace Aw.Context.Core.Lenses;

/// <summary>The distinct records and decisions on an entry's features.</summary>
/// <param name="Records">Records with a 'mentions' edge onto them.</param>
/// <param name="Decisions">Decisions with a 'decides' edge onto them.</param>
public sealed record FileIntent(int Records, int Decisions);
