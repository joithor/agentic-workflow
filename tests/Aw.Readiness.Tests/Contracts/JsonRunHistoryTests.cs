using Aw.Kernel.Testing;
using Aw.Readiness.App;
using Aw.Readiness.App.Infrastructure;
using Aw.Readiness.Core;

namespace Aw.Readiness.Tests.Contracts;

/// <summary>The JSON-file history's own failure paths (integration lane).</summary>
[Lane(TestLane.Integration)]
public sealed class JsonRunHistoryTests : IDisposable
{
    private readonly TempDirectory _state = new();

    public void Dispose() => _state.Dispose();

    [Fact]
    public async Task A_corrupt_run_file_is_AW_READINESS_004_naming_the_file_and_the_fix()
    {
        var settings = SettingsFromEnvironment.Read(name => name == "AW_STATE_DIR" ? _state.Path : null);
        var runs = Directory.CreateDirectory(Path.Combine(settings.StateDirectory, "runs", "agentic-workflow", "csharp-template")).FullName;
        await File.WriteAllTextAsync(Path.Combine(runs, "20261010T000000000000Z-000.json"), "{ not json", TestContext.Current.CancellationToken);

        var latest = await new JsonRunHistory(settings, Composition.Json).Latest(RepoName.From("agentic-workflow"), "csharp-template", CancellationToken.None);

        Assert.Equal("AW-READINESS-004", latest.Error.Code);
        Assert.Contains("20261010T000000000000Z-000.json", latest.Error.Message, StringComparison.Ordinal);
        Assert.Contains("fix:", latest.Error.Message, StringComparison.Ordinal);
    }
}
