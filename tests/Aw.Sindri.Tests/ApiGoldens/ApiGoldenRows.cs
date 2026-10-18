namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// API P1's response bodies per route and scenario against their golden files (UI spec E-17, plan P1 unit E), one class per route group so
/// xUnit runs the groups in parallel (owner, 2026-10-18: the single serial class set the package's lane time). Each row starts its own
/// <see cref="GoldenHost"/>, so a row's answer never depends on which rows ran before it and the groups share nothing.
/// </summary>
internal static class ApiGoldenRows
{
    public static TheoryData<string, string> Of(string group)
    {
        var rows = new TheoryData<string, string>();
        foreach (var (slug, scenario) in ApiScenarios.Groups[group])
        {
            rows.Add(slug, scenario);
        }

        return rows;
    }

    public static async Task Match(string slug, string scenario, CancellationToken ct)
    {
        await using var host = await ApiScenarios.Start(slug, scenario, ct);

        var body = await ApiScenarios.Body(host, slug, scenario, ct);

        ApiGolden.Match(body, host.Home, slug, scenario);
    }
}
