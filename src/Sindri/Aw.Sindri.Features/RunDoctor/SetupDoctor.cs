using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.RunDoctor;

/// <summary>
/// Doctor's 'setup' check (setup-once spec §7.5): ok when every step is done or skipped; otherwise a warning that names each step still open and the owner's next move, the first open step's terminal form.
/// Never a failure: an unfinished setup is not a broken Sindri.
/// </summary>
/// <param name="facts">The sources, read the way GET /setup reads them.</param>
internal sealed class SetupDoctor(ISetupFacts facts) : IDoctorChecks
{
    /// <summary>The check's name.</summary>
    internal const string Name = "setup";

    public async ValueTask<IReadOnlyList<DoctorCheck>> Run(CancellationToken cancellationToken)
    {
        var steps = await new SetupDeriver(facts).Derive(cancellationToken).ConfigureAwait(false);
        var open = steps.Where(static s => s.State is not (SetupState.Done or SetupState.Skipped)).ToList();
        if (open.Count == 0)
        {
            return [new DoctorCheck(Name, CheckState.Ok, "every step is done", Fix: null)];
        }

        var detail = "not finished: " + string.Join(", ", open.Select(static s => $"{SetupStepChanges.Word(s.Id)} ({SetupStepChanges.Word(s.State)})"));
        return [new DoctorCheck(Name, CheckState.Warn, detail, Fix: FixFor(open[0].Id))];
    }

    // The terminal form of the action that moves a step forward; the app's button does the same.
    private static string FixFor(SetupStepId id) => id switch
    {
        SetupStepId.Repo => "sindri repo add <path>",
        SetupStepId.Profile => "sindri profile approve <hash>",
        SetupStepId.Recipes => "sindri recipe approve-all",
        SetupStepId.Mcp => "sindri mcp register --apply",
        SetupStepId.Map => "sindri context map refresh",
        SetupStepId.Onyx => "sindri onyx up",
        _ => throw new ArgumentOutOfRangeException(nameof(id)),
    };
}
