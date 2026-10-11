using Aw.Evidence.Core.Files;
using Aw.Evidence.Core.Guard;
using Aw.Evidence.Core.Stack;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Evidence.Features.Doctor;

/// <summary>
/// Read-only stack health. The app's page title is deployment-specific, so it comes from UI_EVIDENCE_APP_TITLE or the
/// local env file UI_EVIDENCE_CONFIG names; a page on :3000 with another title is a foreign process, never driven.
/// </summary>
internal sealed class DoctorService(IStackProbe probe, ISecretsSource settings, IRunFiles files) : IService<DoctorRequest, DoctorResponse>
{
    internal const string TitleVariable = "UI_EVIDENCE_APP_TITLE";
    internal const string ConfigVariable = "UI_EVIDENCE_CONFIG";
    internal static readonly Uri LoginPage = new("http://localhost:3000/login");

    public async ValueTask<Result<DoctorResponse>> Handle(DoctorRequest request, CancellationToken cancellationToken)
    {
        var failures = new List<string>();
        var title = settings.Get(TitleVariable) ?? await ConfiguredTitleAsync(cancellationToken).ConfigureAwait(false);
        if (title is null)
        {
            failures.Add($"{TitleVariable} is not set: set it in the environment or in the env file {ConfigVariable} names");
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
            failures.Add("postgres_container is not ready");
        }

        if ((await probe.PortOpenAsync("localhost", 5002, cancellationToken).ConfigureAwait(false)).IsError)
        {
            failures.Add("api-server (:5002) is not accepting connections");
        }

        return new DoctorResponse(failures.Count == 0, failures);
    }

    private async ValueTask<string?> ConfiguredTitleAsync(CancellationToken cancellationToken)
    {
        if (settings.Get(ConfigVariable) is not { } path)
        {
            return null;
        }

        var content = await files.ReadAsync(path, cancellationToken).ConfigureAwait(false);
        return content.IsError ? null : LocalEnvFile.Find(content.Value, TitleVariable);
    }
}
