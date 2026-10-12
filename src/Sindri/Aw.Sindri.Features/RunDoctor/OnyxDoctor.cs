using Aw.Context.Core.Facade;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.RunDoctor;

/// <summary>Doctor's 'context:onyx' check (context spec §8.8).</summary>
/// <param name="engine">The context engine.</param>
internal sealed class OnyxDoctor(IContextEngine engine)
{
    /// <summary>Judges the onyx source the approved profile names.</summary>
    /// <param name="approved">The approved profile, or null.</param>
    /// <param name="cancellationToken">Cancels the probe.</param>
    /// <returns>The check.</returns>
    internal ValueTask<DoctorCheck> Check(LoadedProfile? approved, CancellationToken cancellationToken)
    {
        _ = (engine, approved, cancellationToken);
        throw new NotSupportedException("red: context:onyx");
    }
}
