using Aw.BugFix.Core.Ids;
using Aw.BugFix.Core.Model;
using Aw.Kernel;

namespace Aw.BugFix.Core.Ports;

/// <summary>The run records and judge inputs a bug fix writes under its state directory.</summary>
public interface IRunLedger
{
    /// <summary>Reserves the files for one run (unique per call), creating the runs directory.</summary>
    /// <param name="directory">The state directory.</param>
    /// <param name="at">When the run starts.</param>
    /// <param name="kind">The check kind.</param>
    /// <returns>The run's files, or AW-BUGFIX-023 when the bug fix's digest key is missing or malformed.</returns>
    public Result<RunSlot> Reserve(string directory, DateTimeOffset at, CheckKind kind);

    /// <summary>Creates the bug fix's secret digest key beside its state, once: an existing valid key is kept.</summary>
    /// <param name="directory">The state directory.</param>
    public void CreateDigestKey(string directory);

    /// <summary>Writes a run record.</summary>
    /// <param name="slot">The run's files.</param>
    /// <param name="record">The record.</param>
    /// <returns>The record's SHA-256.</returns>
    public string Write(RunSlot slot, RunRecord record);

    /// <summary>Reads a run record back.</summary>
    /// <param name="path">The record's path (relative to the working directory, or full).</param>
    /// <returns>The record with its full path and current hash, or AW-BUGFIX-009 when it isn't a run record.</returns>
    public Result<StoredRun> Read(string path);

    /// <summary>Gives back a reserved slot whose run produced no record, removing what Reserve created.</summary>
    /// <param name="slot">The slot.</param>
    public void Release(RunSlot slot);

    /// <summary>Writes the resolution-check input asked about a candidate.</summary>
    /// <param name="directory">The state directory.</param>
    /// <param name="candidate">The candidate.</param>
    /// <param name="input">The input.</param>
    /// <returns>The question: the input with the written file and its SHA-256.</returns>
    public JudgeQuestion WriteJudgeInput(string directory, CandidateId candidate, ResolutionCheckInput input);
}
