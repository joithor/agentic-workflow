namespace Aw.Sindri.Core.LensPacks;

/// <summary>The wording rules of a style pack (line spec §5.1 'rules'): the phrases spec.md must not hold and its line cap.</summary>
/// <param name="Forbidden">Phrases spec.md must not hold, matched as whole words without regard to case.</param>
/// <param name="MaxLines">The most lines spec.md may hold; null for no cap.</param>
public sealed record StyleRules(IReadOnlyList<string> Forbidden, int? MaxLines);
