using System.Globalization;
using Aw.Context.Core;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Mcp;
using Aw.Sindri.Core.Profile;
using static Aw.Sindri.Features.RegisterMcp.ProviderPlans;

namespace Aw.Sindri.Features.RegisterMcp;

/// <summary>
/// Builds, and with Apply writes, the per-repo registration of /sindri/mcp for each requested provider: Claude Code's local
/// scope, the checkout's .cursor/mcp.json and the checkout's .codex/config.toml. Never a user-wide entry. A provider that
/// fails is a result, not an error, so one failing never stops the others.
/// </summary>
internal sealed class RegisterMcpService(IApprovedProfile approvals, IRepoLocator locator, IEndpointFile endpoint, IClaudeCli claude, IProviderHost host)
    : IService<RegisterMcpRequest, RegisterMcpResponse>
{
    /// <summary>Owner's call (A): also mark the checkout trusted in Codex's user config so its project file loads at once. False: the human trusts the project in Codex (B).</summary>
    internal const bool WriteCodexTrust = true;

    private const string CursorFile = ".cursor/mcp.json";
    private const string CodexFile = ".codex/config.toml";

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
        var port = McpRegistration.Port(running.IsSuccess ? running.Value?.Url : null, loaded.Profile.Api?.Port);
        var arguments = McpRegistration.Arguments(repo, port);
        var url = arguments[arguments.ToList().IndexOf(McpScope.ServerName) + 1];   // why: the header is last, so the URL isn't
        var results = new List<ProviderResult>();
        foreach (var provider in (request.Providers is { Count: > 0 } asked ? asked : [ProviderFile.Claude]).Distinct())
        {
            results.Add(await Register(provider, request.Apply, repo.Value, checkout, url, arguments, cancellationToken).ConfigureAwait(false));
        }

        return new RegisterMcpResponse(repo, checkout, arguments, request.Apply, results);
    }

    private async ValueTask<ProviderResult> Register(ProviderFile provider, bool apply, string repo, string checkout, string url, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        if (!host.Installed(provider))
        {
            return new ProviderResult(provider, ProviderOutcome.Skipped, "not installed", Fix: null);
        }

        try
        {
            return provider switch
            {
                ProviderFile.Claude => await RegisterClaude(apply, repo, checkout, url, arguments, cancellationToken).ConfigureAwait(false),
                ProviderFile.Cursor => await RegisterFile(provider, apply, checkout, CursorFile, Cursor(await host.Read(Path.Combine(checkout, CursorFile), cancellationToken).ConfigureAwait(false), url, repo), trust: false, cancellationToken).ConfigureAwait(false),
                _ => await RegisterFile(provider, apply, checkout, CodexFile, Codex(await host.Read(Path.Combine(checkout, CodexFile), cancellationToken).ConfigureAwait(false), url, repo), trust: true, cancellationToken).ConfigureAwait(false),
            };
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return new ProviderResult(provider, ProviderOutcome.Failed, e.Message, "check the file's permissions in the checkout, then run it again");
        }
    }

    private async ValueTask<ProviderResult> RegisterClaude(bool apply, string repo, string checkout, string url, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var state = Claude(await host.Read(host.UserConfigPath(ProviderFile.Claude), cancellationToken).ConfigureAwait(false), checkout, url, repo);
        var line = McpRegistration.CommandLine(arguments);
        if (state == ClaudeState.Same)
        {
            return new ProviderResult(ProviderFile.Claude, ProviderOutcome.Unchanged, $"local scope in {checkout}", Fix: null);
        }

        if (!apply)
        {
            return new ProviderResult(ProviderFile.Claude, ProviderOutcome.Registered, (state == ClaudeState.Different ? "would replace the entry, then run: " : "would run: ") + line, Fix: null);
        }

        if (state == ClaudeState.Different
            && await claude.Run(["mcp", "remove", "--scope", "local", McpScope.ServerName], checkout, cancellationToken).ConfigureAwait(false) is not { ExitCode: 0 })
        {
            return Failed(checkout, null, "removing the old entry failed", line);
        }

        return await claude.Run(arguments, checkout, cancellationToken).ConfigureAwait(false) switch
        {
            null => Failed(checkout, null, $"the claude CLI couldn't be started in {checkout}", line),
            { ExitCode: not 0 } failed => Failed(checkout, failed.ExitCode, failed.ErrorTail, line),
            _ => new ProviderResult(ProviderFile.Claude, ProviderOutcome.Registered, $"local scope in {checkout}", Fix: null),
        };
    }

    private static ProviderResult Failed(string checkout, int? exit, string tail, string line) => new(
        ProviderFile.Claude,
        ProviderOutcome.Failed,
        exit is { } code ? string.Create(CultureInfo.InvariantCulture, $"claude exited {code}{(tail.Length == 0 ? string.Empty : ": " + tail)}") : tail,
        $"run it by hand in {checkout}: {line} (install Claude Code or set AW_SINDRI_CLAUDE_BIN if it wasn't found)");

    private async ValueTask<ProviderResult> RegisterFile(ProviderFile provider, bool apply, string checkout, string relative, FilePlan plan, bool trust, CancellationToken cancellationToken)
    {
        var path = Path.Combine(checkout, relative);
        if (plan.Problem is { } problem)
        {
            return new ProviderResult(provider, ProviderOutcome.Failed, $"{path} {problem}", $"add the sindri server to it by hand (sindri mcp register prints the entry), or fix the file and run it again");
        }

        var outcome = ProviderOutcome.Unchanged;
        if (plan.Text is { } text)
        {
            if (await host.Tracked(checkout, relative, cancellationToken).ConfigureAwait(false))
            {
                return new ProviderResult(provider, ProviderOutcome.Failed, $"{path} is tracked by git, so it isn't changed silently", $"edit it by hand or untrack it, then run it again");
            }

            if (apply)
            {
                await host.Write(path, text, cancellationToken).ConfigureAwait(false);
            }

            outcome = ProviderOutcome.Registered;
        }

        return trust ? await WithTrust(provider, apply, checkout, path, outcome, cancellationToken).ConfigureAwait(false) : new ProviderResult(provider, outcome, path, Fix: null);
    }

    /// <summary>Codex loads a project file only for a trusted project: add the trust row when there is none, leave any existing level alone.</summary>
    private async ValueTask<ProviderResult> WithTrust(ProviderFile provider, bool apply, string checkout, string path, ProviderOutcome outcome, CancellationToken cancellationToken)
    {
        var userPath = host.UserConfigPath(ProviderFile.Codex);
        var user = await host.Read(userPath, cancellationToken).ConfigureAwait(false);
        var level = CodexTrust(user, checkout);
        if (string.Equals(level, "trusted", StringComparison.Ordinal))
        {
            return new ProviderResult(provider, outcome, path, Fix: null);
        }

        if (level is not null || !WriteCodexTrust)
        {
            var why = level is null ? "the project isn't trusted" : $"project trust is \"{level}\", not changed";
            return new ProviderResult(provider, ProviderOutcome.NeedsTrust, $"{(apply ? "registered" : "would register")}; {why} - Codex won't load {path} until it is trusted", "trust this project in Codex");
        }

        if (apply)
        {
            await host.Write(userPath, CodexTrustRow(user, checkout), cancellationToken).ConfigureAwait(false);
        }

        return new ProviderResult(provider, ProviderOutcome.Registered, $"{(apply ? "registered; trusted" : "would register; trust")} this project in {userPath}", Fix: null);
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
}
