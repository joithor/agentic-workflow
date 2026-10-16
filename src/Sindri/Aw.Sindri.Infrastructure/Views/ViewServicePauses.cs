using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Infrastructure.Views;

/// <summary>The services a human paused (API P6 unit D), read from the 'services' view in views.db: the scheduler asks once per tick, so a pause survives a restart with the views.</summary>
/// <param name="reader">The views.db read side.</param>
internal sealed class ViewServicePauses(IViewReader reader) : IServicePauses
{
    public async ValueTask<Result<IReadOnlySet<ServiceName>>> Paused(CancellationToken cancellationToken)
    {
        var rows = await reader.List<ServiceRow>(ViewNames.Services, cancellationToken).ConfigureAwait(false);
        return rows.IsError ? rows.Error : Results.Success<IReadOnlySet<ServiceName>>(rows.Value.Where(static r => r.Paused).Select(static r => r.Name).ToHashSet());
    }
}
