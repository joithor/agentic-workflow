using Aw.Evidence.Core.Guard;
using Aw.Evidence.Core.Settings;
using Aw.Evidence.Core.Stack;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Evidence.Features.Doctor;

/// <summary>
/// Read-only stack health. The app's page title is deployment-specific, so it comes from the settings; a page on
/// :3000 with another title is a foreign process, never driven.
/// </summary>
internal sealed class DoctorService(IStackProbe probe, IEvidenceSettings settings) : IService<DoctorRequest, DoctorResponse>
{
    internal static readonly Uri LoginPage = new(new Uri(HostGuard.DefaultOrigin), "/login");

    public async ValueTask<Result<DoctorResponse>> Handle(DoctorRequest request, CancellationToken cancellationToken)
    {
        var failures = new List<string>();
        var title = await settings.AppTitleAsync(cancellationToken).ConfigureAwait(false);
        if (title is null)
        {
            failures.Add($"the app title is not set: set {settings.AppTitleSource}");
        }
        else
        {
            var page = await probe.PageTitleAsync(LoginPage, cancellationToken).ConfigureAwait(false);
            if (page.IsError || !string.Equals(page.Value, title, StringComparison.Ordinal))
            {
                failures.Add(":3000 does not look like this app (a foreign process, or the stack is not up): refusing to drive it");
            }
        }

        if ((await probe.DatabaseReadyAsync(cancellationToken).ConfigureAwait(false)).IsError)
        {
            failures.Add("the database is not ready");   // the container behind it is the probe adapter's detail
        }

        if ((await probe.PortOpenAsync("localhost", 5002, cancellationToken).ConfigureAwait(false)).IsError)
        {
            failures.Add("api-server (:5002) is not accepting connections");
        }

        return new DoctorResponse(failures.Count == 0, failures);
    }
}
