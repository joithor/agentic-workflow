namespace Aw.Readiness.Core.Criteria;

/// <summary>A criterion and the check that decides it.</summary>
/// <param name="Criterion">The criterion.</param>
/// <param name="Check">The check.</param>
internal sealed record CatalogueEntry(Criterion Criterion, Func<RepoContext, Verdict> Check)
{
    internal static CatalogueEntry Of(string id, Pillar pillar, int level, string title, string fix, Func<RepoContext, Verdict> check) =>
        new(new Criterion(CriterionId.From(id), pillar, level, title, fix), check);
}
