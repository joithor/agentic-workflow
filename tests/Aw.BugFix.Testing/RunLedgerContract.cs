using Aw.BugFix.Core.Errors;
using Aw.BugFix.Core.Ids;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;
using Xunit;

namespace Aw.BugFix.Testing;

/// <summary>The <see cref="IRunLedger"/> contract, run against the in-memory fake and the JSON file ledger.</summary>
public abstract class RunLedgerContract
{
    private static readonly DateTimeOffset _at = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    private static readonly RunRecord _record = new(CheckKind.Test, RunOutcome.Failed, new string('a', 40), "tests", new string('b', 64), ["bash", "t.sh"], new string('c', 64), new Dictionary<string, string>(StringComparer.Ordinal) { ["PATH"] = "/bin" }, 1, "/log");

    /// <summary>Gets the ledger under test.</summary>
    protected abstract IRunLedger Ledger { get; }

    /// <summary>Gets a state directory.</summary>
    protected abstract string Directory { get; }

    /// <summary>Two runs reserved at the same instant get different files.</summary>
    [Fact]
    public void Runs_reserved_at_the_same_instant_get_different_files()
    {
        var first = Ledger.Reserve(Directory, _at, CheckKind.Test);
        var second = Ledger.Reserve(Directory, _at, CheckKind.Test);

        Expect.That(!Equals(first.Record, second.Record), "NotEqual(first.Record, second.Record)");
        Expect.That(!Equals(first.Log, second.Log), "NotEqual(first.Log, second.Log)");
    }

    /// <summary>A written record reads back with the hash Write returned.</summary>
    [Fact]
    public void A_written_record_reads_back_with_its_hash()
    {
        var slot = Ledger.Reserve(Directory, _at, CheckKind.Test);

        var sha = Ledger.Write(slot, _record);
        var stored = Ledger.Read(slot.Record).Value;

        Expect.Equal((slot.Record, sha), (stored.Path, stored.Sha256), "Equal((slot.Record, sha), (stored.Path, stored.Sha256))");
        Expect.Equal(_record with { Command = null, Environment = stored.Record.Environment }, stored.Record with { Command = null }, "Equal(_record with { Command = null }, stored.Record with { Command = null })");
        Expect.That((stored.Record.Command ?? []).SequenceEqual(_record.Command ?? [], StringComparer.Ordinal), "the command round-trips");
    }

    /// <summary>An edited record reads back with a different hash.</summary>
    [Fact]
    public void An_edited_record_has_a_new_hash()
    {
        var slot = Ledger.Reserve(Directory, _at, CheckKind.Test);
        var sha = Ledger.Write(slot, _record);

        Edit(slot.Record, _record with { Outcome = RunOutcome.Passed, ExitCode = 0 });

        Expect.That(!Equals(sha, Ledger.Read(slot.Record).Value.Sha256), "NotEqual(sha, Ledger.Read(slot.Record).Value.Sha256)");
    }

    /// <summary>Something that isn't a run record is AW-BUGFIX-009.</summary>
    [Fact]
    public void A_missing_record_is_AW_BUGFIX_009() =>
        Expect.Equal(BugFixErrors.EvidenceRefused.Code, Ledger.Read($"{Directory}/runs/none.json").Error.Code, "Equal(BugFixErrors.EvidenceRefused.Code, Ledger.Read($'{Directory}/runs/none.json').Error.Code)");

    /// <summary>A released slot leaves no record behind.</summary>
    [Fact]
    public void A_released_slot_leaves_no_record()
    {
        var slot = Ledger.Reserve(Directory, _at, CheckKind.Ui);

        Ledger.Release(slot);

        Expect.Equal(BugFixErrors.EvidenceRefused.Code, Ledger.Read(slot.Record).Match(static _ => "read", static e => e.Code), "nothing to read where the slot was");
    }

    /// <summary>A judge input write names its file and returns the same hash for the same input.</summary>
    [Fact]
    public void A_judge_input_hash_is_stable()
    {
        var input = new ResolutionCheckInput("b", "e", "a", "r", CheckKind.Test, "s", BeforePassed: false, AfterPassed: true, "stat", Diff: null);

        var first = Ledger.WriteJudgeInput(Directory, CandidateId.From("c1"), input);
        Expect.That(first.InputFile.EndsWith("judge-c1.json", StringComparison.Ordinal), "the input file is judge-<candidate>.json");
        Expect.Equal(first.InputSha256, Ledger.WriteJudgeInput(Directory, CandidateId.From("c1"), input).InputSha256, "Equal(Ledger.WriteJudgeInput(Directory, CandidateId.From('c1'), input), Ledger.WriteJudgeInput(Directory, Cand");
    }

    /// <summary>Replaces the record at <paramref name="path"/>, as an agent editing evidence would.</summary>
    /// <param name="path">The record's path.</param>
    /// <param name="record">The edited record.</param>
    protected abstract void Edit(string path, RunRecord record);
}
