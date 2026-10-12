using System.Globalization;
using Aw.Context.Core;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Mcp;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.RegisterMcp;

/// <summary>Builds, and with Apply runs, the per-repo Claude Code registration of /sindri/mcp. Never user-wide; never Codex or Cursor.</summary>
internal sealed class RegisterMcpService(IApprovedProfile approvals, IRepoLocator locator, IEndpointFile endpoint, IClaudeCli claude)
    : IService<RegisterMcpRequest, RegisterMcpResponse>
{
    public async ValueTask<Result<RegisterMcpResponse>> Handle(RegisterMcpRequest request, CancellationToken cancellationToken)
    {
        var approved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        if (approved.IsError || approved.Value is not { } loaded)
        {
            return approved.IsError ? approved.Error : SindriErrors.ProfileNotApproved.With("no profile is approved, so no repo is mapped (fix: sindri profile approve)");
        }

        var repos = loaded.Profile.Repos ?? [];
        var entry = await Entry(request, repos, cancellationToken).ConfigureAwait(false);
        if (entry.IsError)
        {
            return entry.Error;
        }

        var running = await endpoint.Read(cancellationToken).ConfigureAwait(false);
        var (repo, checkout) = entry.Value;
        var arguments = McpRegistration.Arguments(repo, McpRegistration.Port(running.IsSuccess ? running.Value?.Url : null, loaded.Profile.Api?.Port));
        return request.Apply
            ? Applied(await claude.Run(arguments, checkout, cancellationToken).ConfigureAwait(false), new RegisterMcpResponse(repo, checkout, arguments, Applied: true))
            : new RegisterMcpResponse(repo, checkout, arguments, Applied: false);
    }

    /// <summary>The named repo, or the one the directory is in (a worktree resolves to its main checkout); else SND-CONTEXT-024.</summary>
    private async ValueTask<Result<(RepoName Repo, string Checkout)>> Entry(RegisterMcpRequest request, IReadOnlyList<RepoEntry> repos, CancellationToken cancellationToken)
    {
        var name = request.Repo is { } named
            ? (RepoName.TryFrom(named, out var parsed) ? parsed : null)
            : await locator.Locate(request.Directory, repos, cancellationToken).ConfigureAwait(false);
        if (name is { } repo && repos.FirstOrDefault(r => string.Equals(r.Name, repo.Value, StringComparison.Ordinal)) is { } entry)
        {
            return (repo, entry.Path);
        }

        return ContextErrors.McpRegisterRepoUnknown.With(request.Repo is { } asked
            ? $"'{asked}' is not a repo the profile names (fix: one of: {string.Join(", ", repos.Select(r => r.Name))})"
            : $"{request.Directory} is in no repo the profile names (fix: run it in the work repo, or pass --repo NAME)");
    }

    private static Result<RegisterMcpResponse> Applied(ClaudeRun? run, RegisterMcpResponse registered) => run switch
    {
        null => ContextErrors.McpRegisterFailed.With("the claude CLI couldn't be started (fix: install Claude Code, or set AW_SINDRI_CLAUDE_BIN; or run the printed command by hand)"),
        { ExitCode: not 0 } failed => ContextErrors.McpRegisterFailed.With(
            string.Create(CultureInfo.InvariantCulture, $"claude exited {failed.ExitCode}: {failed.ErrorTail} (fix: run the printed command by hand to see why)")),
        _ => registered,
    };
}
