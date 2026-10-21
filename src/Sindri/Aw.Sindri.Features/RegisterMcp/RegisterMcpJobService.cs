using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Mcp;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.RegisterMcp;

/// <summary>
/// The work of a RegisterMcp job (setup-once spec §5.4): the registration slice for the repo the admission resolved, on every provider (the ones not installed are skipped), written for real, and without Codex's
/// project-trust row (Codex asks the owner once). It names the repo, so it never reads the current directory.
/// </summary>
internal sealed class RegisterMcpJobService(ResolvedMcpRepos resolved, IApprovedProfile approvals, IRepoLocator locator, IEndpointFile endpoint, IClaudeCli claude, IProviderHost host, IJobContextAccessor jobs)
    : IService<RegisterMcpJobRequest, RegisterMcpResponse>
{
    public async ValueTask<Result<RegisterMcpResponse>> Handle(RegisterMcpJobRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (resolved.Take(request) is not { } repo)
        {
            return SindriErrors.ApiInternal.With("the RegisterMcp job has no resolved repo");
        }

        jobs.Current?.Progress.Report("registering");
        var service = new RegisterMcpService(approvals, locator, endpoint, claude, host) { WriteCodexTrust = false };
        return await service.Handle(new RegisterMcpRequest(Directory: string.Empty, repo.Value, Apply: true, [ProviderFile.Claude, ProviderFile.Cursor, ProviderFile.Codex]), cancellationToken).ConfigureAwait(false);
    }
}
