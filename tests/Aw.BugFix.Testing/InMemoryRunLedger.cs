using System.Globalization;
using Aw.BugFix.Core.Errors;
using Aw.BugFix.Core.Ids;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;
using Aw.Kernel;

namespace Aw.BugFix.Testing;

/// <summary>The in-memory fake for <see cref="IRunLedger"/>: records by full path, with a version number standing in for the hash.</summary>
public sealed class InMemoryRunLedger : IRunLedger
{
    private readonly Dictionary<string, (RunRecord Record, int Version)> _records = new(StringComparer.Ordinal);
    private int _slots;

    /// <summary>Gets the resolution-check inputs written, by candidate.</summary>
    public IDictionary<CandidateId, ResolutionCheckInput> JudgeInputs { get; } = new Dictionary<CandidateId, ResolutionCheckInput>();

    /// <summary>Changes a record after it was written, as an agent editing evidence would.</summary>
    /// <param name="path">The record's full path.</param>
    /// <param name="record">The edited record.</param>
    public void Tamper(string path, RunRecord record) => _records[path] = (record, _records[path].Version + 1);

    /// <summary>Puts a record nobody registered at <paramref name="path"/>, as a hand-written evidence file would be.</summary>
    /// <param name="path">The full path.</param>
    /// <param name="record">The record.</param>
    public void Forge(string path, RunRecord record) => _records[path] = (record, 0);

    /// <inheritdoc/>
    public RunSlot Reserve(string directory, DateTimeOffset at, CheckKind kind)
    {
        var stem = string.Create(CultureInfo.InvariantCulture, $"{directory.TrimEnd('/')}/runs/{++_slots:D3}-{(kind == CheckKind.Test ? "test" : "ui")}");
        return new RunSlot(kind == CheckKind.Test ? $"{stem}.log" : stem, $"{stem}.log", $"{stem}.json");
    }

    /// <inheritdoc/>
    public string Write(RunSlot slot, RunRecord record)
    {
        _records[slot.Record] = (record, 1);
        return Hash(slot.Record, 1);
    }

    /// <inheritdoc/>
    public Result<StoredRun> Read(string path) =>
        _records.TryGetValue(path, out var entry)
            ? new StoredRun(path, Hash(path, entry.Version), entry.Record)
            : BugFixErrors.EvidenceRefused.With($"cannot read evidence: {path}");

    /// <summary>Gets the slots reserved and released, by record path.</summary>
    public ISet<string> Released { get; } = new HashSet<string>(StringComparer.Ordinal);

    /// <inheritdoc/>
    public void Release(RunSlot slot) => _ = Released.Add(slot.Record);

    /// <inheritdoc/>
    public JudgeQuestion WriteJudgeInput(string directory, CandidateId candidate, ResolutionCheckInput input)
    {
        JudgeInputs[candidate] = input;
        var file = $"{directory.TrimEnd('/')}/judge-{candidate.Value}.json";
        return new JudgeQuestion(input, file, Hash(file, input.GetHashCode()));
    }

    private static string Hash(string path, int version) =>
        $"{path.GetHashCode(StringComparison.Ordinal):x8}{version:x56}";
}
