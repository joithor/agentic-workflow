using Aw.Sindri.Core.Doctor;

namespace Aw.Sindri.Infrastructure.Deploy;

/// <summary>Doctor's read-only echo of the deploy mode (deployable-context spec A2a): a row in pod mode, none on the desktop.</summary>
/// <param name="settings">Holds the mode.</param>
internal sealed class DeployDoctorChecks(SindriSettings settings) : IDoctorChecks
{
    public ValueTask<IReadOnlyList<DoctorCheck>> Run(CancellationToken cancellationToken)
        => ValueTask.FromResult<IReadOnlyList<DoctorCheck>>(settings.Deploy == DeployMode.Pod
            ? [new DoctorCheck("deploy:mode", CheckState.Ok, "pod mode (AW_SINDRI_DEPLOY=pod)", Fix: null)]
            : []);
}
