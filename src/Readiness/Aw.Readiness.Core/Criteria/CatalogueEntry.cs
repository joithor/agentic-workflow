namespace Aw.Readiness.Core.Criteria;

/// <summary>A criterion and the check that decides it.</summary>
/// <param name="Criterion">The criterion.</param>
/// <param name="Check">The check.</param>
internal sealed record CatalogueEntry(Criterion Criterion, Func<RepoContext, Verdict> Check)
{
    internal static CatalogueEntry Of(CriterionKey key, string title, string fix, Func<RepoContext, Verdict> check) =>
        new(new Criterion(CriterionId.From(key.Id), key.Pillar, key.Level, title, fix), check);
}
