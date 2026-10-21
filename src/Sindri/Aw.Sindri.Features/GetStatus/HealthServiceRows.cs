using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.GetStatus;

/// <summary>The 'services' rows health judges: those of the approved profile's services and of the build's built-ins (<see cref="ApprovedServices"/>).</summary>
/// <param name="views">Reads the services view.</param>
/// <param name="builtIns">The build's built-in services.</param>
internal sealed class HealthServiceRows(IViewReader views, IEnumerable<BuiltInService> builtIns)
{
    /// <summary>The rows that count for health.</summary>
    /// <param name="approved">The approved profile, or null before the first approval.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The rows, or null when the view can't be read.</returns>
    public async ValueTask<IReadOnlyList<ServiceRow>?> Read(LoadedProfile? approved, CancellationToken cancellationToken)
    {
        var rows = await views.List<ServiceRow>(ViewNames.Services, cancellationToken).ConfigureAwait(false);
        return rows.IsSuccess ? ApprovedServices.Of(rows.Value, approved, builtIns) : null;
    }
}
