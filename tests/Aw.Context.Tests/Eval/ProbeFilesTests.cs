using Aw.Context.Core;
using Aw.Context.Core.Eval;
using Aw.Context.Infrastructure.Eval;
using Aw.Context.Infrastructure.Store;
using Aw.Context.Testing.Fixtures;
using Aw.Context.Tests.Map;
using Aw.Kernel;
using TempGitRepo = Aw.Context.Tests.Infrastructure.TempGitRepo;

namespace Aw.Context.Tests.Eval;

/// <summary>The probe file reader and the reports folder on disk (spec §3: nothing inside a worktree; files 0600 in 0700 folders).</summary>
[Trait("Lane", "integration")]
public sealed class ProbeFilesTests : IDisposable
{
    private readonly TempHome _home = new();
    private readonly ContextStoreSettings _settings;

    public ProbeFilesTests()
    {
        _settings = SettingsFromEnvironment.Read(_home.Environment);
    }

    public void Dispose() => _home.Dispose();

    private static CancellationToken None => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_fixture_probe_file_reads_with_its_hash_and_a_bad_missing_or_worktree_file_is_refused()
    {
        var reader = new ProbeFileReader(_settings);
        var good = Place("probes.json", SplitPanesFixture.ReadText("probes.json"));
        var bad = Place("bad.json", """{"probes":[{"id":"a","question":"q","expect":{},"extra":1}]}""");
        var repo = Path.Combine(_home.Root, "repo");
        _ = Directory.CreateDirectory(Path.Combine(repo, ".git"));
        var inRepo = Path.Combine(repo, "probes.json");
        File.Copy(good, inRepo);

        var read = await reader.Read(good, None);
        var codes = new[]
        {
            (await reader.Read(bad, None)).Error.Code,
            (await reader.Read(Path.Combine(_home.Root, "nope.json"), None)).Error.Code,
            (await reader.Read(path: null, None)).Error.Code,
            (await reader.Read(inRepo, None)).Error.Code,
        };

        Assert.Equal((8, 64), (read.Value.Set.Probes.Count, read.Value.Hash.Length));
        Assert.Equal([ContextErrors.ProbeFileInvalid.Code, ContextErrors.NoProbeFile.Code, ContextErrors.NoProbeFile.Code, ContextErrors.PathInWorktree.Code], codes);
    }

    [Fact]
    public async Task An_unknown_key_is_SND_CONTEXT_035_at_its_key_path_without_the_parser_s_type_names()
    {
        var reader = new ProbeFileReader(_settings);
        var bad = Place("unknown.json", """{"probes":[{"id":"a","question":"q","expect":{"nodez":[]}}]}""");

        var error = (await reader.Read(bad, None)).Error;

        Assert.Equal(ContextErrors.ProbeFileInvalid.Code, error.Code);
        Assert.Contains(": probes[0].expect.nodez: unknown key; nothing was run", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Aw.Context", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_report_is_written_0600_in_a_0700_folder_and_the_baseline_is_the_newest_of_the_same_file()
    {
        var store = new FileProbeReportStore(_settings);
        var at = new DateTimeOffset(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);

        var first = await store.Write(Report("hash-a", 0.4, at), at, None);
        _ = await store.Write(Report("hash-a", 0.6, at.AddMinutes(1)), at.AddMinutes(1), None);
        _ = await store.Write(Report("hash-b", 0.9, at.AddMinutes(2)), at.AddMinutes(2), None);
        var baseline = await store.Baseline("hash-a", None);
        var none = await store.Baseline("hash-c", None);

        Assert.StartsWith(_settings.ProbesDirectory, first.Value, StringComparison.Ordinal);
        Assert.Equal(0.6, baseline.Value?.Totals.PrecisionAt5);
        Assert.Null(none.Value);
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(first.Value));
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(_settings.ProbesDirectory));
        }
    }

    [Fact]
    public async Task A_report_is_refused_with_005_inside_a_git_worktree_and_nothing_is_written()
    {
        using var repo = new TempGitRepo(new Dictionary<string, string>(StringComparer.Ordinal) { ["a.txt"] = "a" });
        var inside = new ContextStoreSettings(Path.Combine(repo.Path, "ctx"));
        var at = new DateTimeOffset(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);

        var written = await new FileProbeReportStore(inside).Write(Report("hash-a", 0.4, at), at, None);

        Assert.Equal(ContextErrors.PathInWorktree.Code, written.Error.Code);
        Assert.False(Directory.Exists(Path.Combine(repo.Path, "ctx")));
    }

    [Fact]
    public async Task The_baseline_is_the_newest_complete_run_that_did_not_regress()
    {
        var store = new FileProbeReportStore(_settings);
        var at = new DateTimeOffset(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);
        _ = await store.Write(Report("hash-a", 0.6, at), at, None);
        _ = await store.Write(Report("hash-a", 0.2, at.AddMinutes(1)) with { Regressed = true }, at.AddMinutes(1), None);
        var failed = Report("hash-a", 0.1, at.AddMinutes(2)) with { Probes = [new ProbeScore(ProbeId.From("p"), 0, 0, [], [], [], "SND-CONTEXT-011")] };
        _ = await store.Write(failed, at.AddMinutes(2), None);

        var baseline = await store.Baseline("hash-a", None);

        Assert.Equal(0.6, baseline.Value?.Totals.PrecisionAt5);
    }

    [Fact]
    public async Task A_file_that_is_not_JSON_among_the_reports_is_skipped_and_the_rest_still_read()
    {
        var store = new FileProbeReportStore(_settings);
        var at = new DateTimeOffset(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);
        _ = await store.Write(Report("hash-a", 0.6, at), at, None);
        await File.WriteAllTextAsync(Path.Combine(_settings.ProbesDirectory, "probes-20991231T000000000Z.json"), "not json", None);

        var baseline = await store.Baseline("hash-a", None);

        Assert.Equal(0.6, baseline.Value?.Totals.PrecisionAt5);
    }

    [Fact]
    public async Task A_report_with_both_the_baseline_and_the_previous_key_keeps_the_baseline()
    {
        var store = new FileProbeReportStore(_settings);
        var at = new DateTimeOffset(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);
        _ = await store.Write(Report("hash-a", 0.6, at) with { Baseline = new ProbeTotals(0.5, 1.0, 1, at.AddDays(-1)) }, at, None);
        var written = Directory.EnumerateFiles(_settings.ProbesDirectory).Single();
        var json = (await File.ReadAllTextAsync(written, None)).Replace("\"baseline\":", "\"previous\":{\"precisionAt5\":0.1,\"citationCorrectness\":1,\"probes\":1,\"at\":\"2026-10-13T09:00:00Z\",\"absentReturned\":0},\"baseline\":", StringComparison.Ordinal);
        await File.WriteAllTextAsync(written, json, None);

        var baseline = await store.Baseline("hash-a", None);

        Assert.Equal(0.5, baseline.Value?.Baseline?.PrecisionAt5);
    }

    [Fact]
    public async Task A_report_written_before_the_rename_still_reads_with_its_previous_totals_as_the_baseline()
    {
        var store = new FileProbeReportStore(_settings);
        var at = new DateTimeOffset(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);
        _ = await store.Write(Report("hash-a", 0.6, at) with { Baseline = new ProbeTotals(0.5, 1.0, 1, at.AddDays(-1)) }, at, None);
        var written = Directory.EnumerateFiles(_settings.ProbesDirectory).Single();
        var json = (await File.ReadAllTextAsync(written, None)).Replace("\"baseline\":", "\"previous\":", StringComparison.Ordinal);
        Assert.Contains("\"previous\":", json, StringComparison.Ordinal);
        await File.WriteAllTextAsync(written, json, None);

        var baseline = await store.Baseline("hash-a", None);

        Assert.Equal((0.6, 0.5), (baseline.Value?.Totals.PrecisionAt5, baseline.Value?.Baseline?.PrecisionAt5));
    }

    [Fact]
    public async Task A_regressed_run_then_an_identical_rerun_is_SND_CONTEXT_038_both_times()
    {
        var store = new FileProbeReportStore(_settings);
        var clock = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(new DateTimeOffset(2026, 10, 14, 9, 0, 0, TimeSpan.Zero));
        var service = new Features.EvalProbes.EvalProbesService(new OneFile(), store, clock);
        async Task<ProbeReport> Run(bool good)
        {
            clock.Advance(TimeSpan.FromMinutes(1));
            ValueTask<Result<Context.Core.Results.ContextResult>> Ask(ProbeCase p, CancellationToken ct) =>
                ValueTask.FromResult(Results.Success(good ? Answers.Of(("graph", Answers.Item("feature:x"))) : Answers.Of()));
            return (await service.Handle(new Features.EvalProbes.EvalProbesRequest(File: null, Ask), None)).Value.Report;
        }

        var first = await Run(good: true);
        var worse = await Run(good: false);
        var again = await Run(good: false);

        Assert.Equal((false, true, true), (first.Regressed, worse.Regressed, again.Regressed));
    }

    private sealed class OneFile : IProbeFileReader
    {
        public ValueTask<Result<Context.Core.Eval.ProbeFile>> Read(string? path, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<Context.Core.Eval.ProbeFile>>(new Context.Core.Eval.ProbeFile("/probes.json", "hash-x", new ProbeSet([Answers.Probe("p1", nodes: ["feature:x"])])));
    }

    private string Place(string name, string text)
    {
        var path = Path.Combine(_home.Root, name);
        File.WriteAllText(path, text);
        return path;
    }

    private static ProbeReport Report(string hash, double precision, DateTimeOffset at) =>
        new(ProbeReport.CurrentSchemaVersion, "/probes.json", hash, "v1", [], new ProbeTotals(precision, 1.0, 1, at), Baseline: null, Regressed: false);
}
