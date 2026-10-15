using Aw.Meta.Core;
using Aw.Meta.Core.Gate;
using Aw.Meta.Core.Obligations;
using Aw.Meta.Core.Ports;
using Xunit;

namespace Aw.Meta.Testing;

/// <summary>The IGateStore contract: the same tests run against the in-memory fake and every real adapter.</summary>
public abstract class GateStoreContract
{
    private static readonly CancellationToken _none = CancellationToken.None;

    /// <summary>A fresh, empty store.</summary>
    /// <returns>The store.</returns>
    protected abstract ValueTask<IGateStore> Create();

    private static GateRecord Gate(int pr, string head, GateVerdict verdict = GateVerdict.Fail) => new(
        pr,
        head,
        "main1",
        ["repo", "dotnet"],
        [new GateStep("repo", GateVerdict.Pass, "/logs/repo.log"), new GateStep("unit", GateVerdict.Fail, "/logs/unit.log")],
        verdict,
        A.At(5));

    /// <summary>Runs are numbered per pull request and the latest one is returned with its steps in order.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Runs_are_numbered_per_pull_request_and_the_latest_is_returned()
    {
        var store = await Create();

        var first = await store.Save(Gate(190, "h1"), _none);
        var second = await store.Save(Gate(190, "h2"), _none);
        var other = await store.Save(Gate(191, "h9"), _none);
        var latest = await store.Latest(190, _none);
        var none = await store.Latest(404, _none);

        Expect.That(first.Value.Run == 1 && second.Value.Run == 2 && other.Value.Run == 1, "runs count per pull request");
        Expect.That(latest.Value == second.Value, "the latest run reads back with every field");
        Expect.That(latest.Value!.Steps.Select(s => s.Name).SequenceEqual(["repo", "unit"], StringComparer.Ordinal), "steps keep their order");
        Expect.That(none.IsSuccess && none.Value is null, "no run is null");
    }

    /// <summary>Classifying a failed step is stored; an unknown step is AW-META-007.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_failed_step_can_be_classified()
    {
        var store = await Create();
        var saved = await store.Save(Gate(190, "h1"), _none);

        var done = await store.Classify(190, saved.Value.Run, "unit", FlakeVerdict.Flake, _none);
        var wrongStep = await store.Classify(190, saved.Value.Run, "nope", FlakeVerdict.Flake, _none);
        var wrongRun = await store.Classify(190, 9, "unit", FlakeVerdict.Flake, _none);
        var latest = await store.Latest(190, _none);

        Expect.That(done.IsSuccess && latest.Value!.Steps[1].Flake == FlakeVerdict.Flake && latest.Value.Steps[0].Flake is null, "the classification is stored on the step");
        Expect.That(wrongStep.IsError && wrongStep.Error.Code == MetaErrors.GateStepNotFound.Code, "AW-META-007 for an unknown step");
        Expect.That(wrongRun.IsError && wrongRun.Error.Code == MetaErrors.GateStepNotFound.Code, "AW-META-007 for an unknown run");
    }

    /// <summary>The latest merge is the one that merged last.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task The_latest_merge_is_the_most_recent()
    {
        var store = await Create();
        var none = await store.LatestMerge(_none);
        var older = new MergeRecord(188, "aaaaaaa1111", A.At(1));
        var newer = new MergeRecord(190, "bbbbbbb2222", A.At(9));

        _ = await store.RecordMerge(newer, _none);
        _ = await store.RecordMerge(older, _none);
        var latest = await store.LatestMerge(_none);

        Expect.That(none.IsSuccess && none.Value is null, "no merge is null");
        Expect.That(latest.Value == newer, "the most recent merge wins");
    }
}
