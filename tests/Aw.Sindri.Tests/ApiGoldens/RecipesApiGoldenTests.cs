namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>The Recipes group of API goldens (<see cref="ApiScenarios.Groups"/>): approve-all's plan and its job's result.</summary>
public sealed class RecipesApiGoldenTests
{
    public static TheoryData<string, string> Rows() => ApiGoldenRows.Of("Recipes");

    [Theory(Timeout = 30_000), Trait("Lane", "integration")]
    [MemberData(nameof(Rows))]
    public Task Api_answer_matches_its_golden_file(string slug, string scenario) => ApiGoldenRows.Match(slug, scenario, TestContext.Current.CancellationToken);
}
