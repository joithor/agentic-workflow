using Aw.Kernel.Testing;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// API P1's response bodies per route and scenario against their golden files (UI spec E-17, plan P1 unit E). Each row
/// starts its own <see cref="GoldenHost"/>, so a row's answer never depends on which rows ran before it.
/// </summary>
public sealed class ApiGoldenTests
{
    public static TheoryData<string, string> Rows()
    {
        var rows = new TheoryData<string, string>();
        foreach (var (slug, scenario) in ApiScenarios.All)
        {
            rows.Add(slug, scenario);
        }

        return rows;
    }

    [Theory(Timeout = 30_000), Trait("Lane", "integration")]
    [MemberData(nameof(Rows))]
    public async Task Api_answer_matches_its_golden_file(string slug, string scenario)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ApiScenarios.Start(slug, scenario, ct);

        var body = await ApiScenarios.Body(host, slug, scenario, ct);

        ApiGolden.Match(body, host.Home, slug, scenario);
    }

    [Fact, Trait("Lane", "integration")]
    public void Every_golden_file_has_a_scenario()
    {
        var root = Golden.SourcePath(typeof(ApiGoldenTests).Assembly, "Golden/Api");
        var files = Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(root, f).Replace(Path.DirectorySeparatorChar, '/'))
            .Order(StringComparer.Ordinal);
        var expected = ApiScenarios.All.Concat(WorkflowViewGoldenTests.All).Concat(ServedWorkflowRoutesTests.Goldens).Select(r => $"{r.Slug}/{r.Scenario}.json").Order(StringComparer.Ordinal);

        Assert.Equal(expected, files);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("/sindri/status", "status")]
    [InlineData("/sindri/profile/approve", "profile-approve")]
    [InlineData("/sindri/items/{itemId}", "items-id")]
    [InlineData("/sindri/items/{id:long}", "items-id")]
    [InlineData("/sindri/workflow/instances/{instanceId}/nonce", "workflow-instances-id-nonce")]
    public void Route_slug_is_the_path_after_sindri_with_slashes_as_dashes_and_each_parameter_as_id(string route, string slug) =>
        Assert.Equal(slug, ApiGolden.Slug(route));
}
