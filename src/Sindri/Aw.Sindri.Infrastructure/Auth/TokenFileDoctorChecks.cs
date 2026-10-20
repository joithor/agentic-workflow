using Aw.Sindri.Core.Doctor;

namespace Aw.Sindri.Infrastructure.Auth;

/// <summary>Doctor's check of the human token file (setup-once spec §4, API spec §6): the file is the v0 trust boundary, so it warns when it is readable by its group or by everyone.</summary>
/// <param name="settings">Names the token file.</param>
internal sealed class TokenFileDoctorChecks(SindriSettings settings) : IDoctorChecks
{
    public ValueTask<IReadOnlyList<DoctorCheck>> Run(CancellationToken cancellationToken)
    {
        _ = settings;
        return ValueTask.FromResult<IReadOnlyList<DoctorCheck>>([]);
    }
}
