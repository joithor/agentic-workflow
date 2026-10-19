
namespace Aw.Sindri.Features.Scoping;

/// <summary>What a Step's code produced: its outcome, the facts it writes, files for the run area, and its findings.</summary>
/// <param name="Block">The full Step id.</param>
/// <param name="Outcome">The outcome the desk 'complete' reports.</param>
/// <param name="Outputs">The facts written, as JSON text by key without the 'fact:' prefix.</param>
/// <param name="Files">Files for the run area (paths relative to it): the evidence step's packet, and rounds.jsonl lines to append.</param>
/// <param name="Findings">The failing findings, when the Step reports any.</param>
/// <param name="Warnings">Warnings shown beside the report.</param>
public sealed record ScopingDeskResponse(
    string Block,
    string Outcome,
    IReadOnlyDictionary<string, string> Outputs,
    IReadOnlyList<ScopingDeskFile> Files,
    IReadOnlyList<ScopeFinding> Findings,
    IReadOnlyList<ScopeFinding> Warnings);
