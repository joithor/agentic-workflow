using Aw.Context.Core;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Mcp;
using Aw.Sindri.Features.RegisterMcp;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri mcp register [--repo NAME] [--providers claude,codex,cursor|all] [--apply] [--json]' (context spec §3, API spec
/// §8): prints each provider's per-repo registration of /sindri/mcp (Claude Code's local scope, the checkout's
/// .cursor/mcp.json and .codex/config.toml; Claude Code alone without --providers); --apply writes them, for a human at a
/// terminal only (it writes the human's own provider config). Local: it needs no running process.
/// </summary>
/// <param name="services">The CLI's scoped services.</param>
/// <param name="output">Where answers go.</param>
/// <param name="session">Who runs the CLI.</param>
internal sealed class McpVerbs(IServiceProvider services, Output output, AgentSession session)
{
    private const string Usage = "mcp register [--repo NAME] [--providers claude,codex,cursor|all] [--apply] [--json]";

    internal async Task<int> Run(string[] args, CancellationToken ct)
    {
        if (args is not ["register", .. var rest] || Options(rest) is not { } options)
        {
            return await output.Error(SindriErrors.UnknownVerb.With($"'mcp {string.Join(' ', args)}' is not an mcp command: {Usage}")).ConfigureAwait(false);
        }

        if (options.Apply && !session.Human)
        {
            return await Refused(SindriErrors.AuthRefused.With(
                $"'mcp register --apply' writes your providers' config, so only a human at a terminal runs it (this CLI ran as a session: {session.SessionReason}; fix: run 'sindri mcp register' and give the human the printed command)")).ConfigureAwait(false);
        }

        var answer = await services.GetRequiredService<IService<RegisterMcpRequest, RegisterMcpResponse>>()
            .Handle(new RegisterMcpRequest(Environment.CurrentDirectory, options.Repo, options.Apply, options.Providers), ct).ConfigureAwait(false);
        if (answer.IsError)
        {
            return answer.Error.Code == ContextErrors.McpRegisterRepoUnknown.Code
                ? await Refused(answer.Error).ConfigureAwait(false)
                : await output.Error(answer.Error).ConfigureAwait(false);
        }

        var failed = answer.Value.Providers.Where(p => p.Outcome == ProviderOutcome.Failed).ToList();
        var exit = options.Json ? await output.Json(answer.Value, failed.Count == 0 ? 0 : 2).ConfigureAwait(false) : await output.Text(Text(answer.Value), failed.Count == 0 ? 0 : 2).ConfigureAwait(false);
        foreach (var provider in options.Json ? [] : failed)
        {
            _ = await output.Error(ContextErrors.McpRegisterFailed.With($"{Name(provider.Provider)}: {provider.Detail} (fix: {provider.Fix})")).ConfigureAwait(false);
        }

        return exit;
    }

    /// <summary>A refused input (spec §8.6): exit 1, attention, rather than a tool error.</summary>
    private async Task<int> Refused(Kernel.Error error)
    {
        _ = await output.Error(error).ConfigureAwait(false);
        return 1;
    }

    /// <summary>The command as one shell line, each argument quoted where it needs it.</summary>
    internal static string Line(IReadOnlyList<string> arguments) => McpRegistration.CommandLine(arguments);

    private static string Name(ProviderFile provider) => provider switch
    {
        ProviderFile.Claude => "claude",
        ProviderFile.Codex => "codex",
        _ => "cursor",
    };

    private static string Text(RegisterMcpResponse registration)
    {
        var text = new System.Text.StringBuilder();
        text.Append(registration.Applied ? "registered Sindri's /sindri/mcp for " : "would register Sindri's /sindri/mcp for ")
            .Append(registration.Repo.Value).Append(" (").Append(registration.Checkout).Append("):\n");
        foreach (var provider in registration.Providers)
        {
            var outcome = provider.Outcome switch
            {
                ProviderOutcome.NeedsTrust => "registered, needs trust",
                ProviderOutcome.Failed => "failed",
                ProviderOutcome.Skipped => "skipped",
                ProviderOutcome.Unchanged => "unchanged",
                _ => registration.Applied ? "registered" : "would register",
            };
            text.Append("  ").Append(Name(provider.Provider)).Append(": ").Append(outcome).Append(" - ").Append(provider.Detail).Append('\n');
            if (provider.Fix is { } fix && provider.Outcome != ProviderOutcome.Failed)
            {
                text.Append("    fix: ").Append(fix).Append('\n');
            }
        }

        return text + (registration.Applied ? string.Empty : "A human at a terminal can write them with: sindri mcp register --apply\n");
    }

    private static IReadOnlyList<ProviderFile>? ParseProviders(string value)
    {
        if (string.Equals(value, "all", StringComparison.Ordinal))
        {
            return [ProviderFile.Claude, ProviderFile.Codex, ProviderFile.Cursor];
        }

        var providers = new List<ProviderFile>();
        foreach (var name in value.Split(','))
        {
            switch (name)
            {
                case "claude": providers.Add(ProviderFile.Claude); break;
                case "codex": providers.Add(ProviderFile.Codex); break;
                case "cursor": providers.Add(ProviderFile.Cursor); break;
                default: return null;
            }
        }

        return providers;
    }

    private static (string? Repo, bool Apply, bool Json, IReadOnlyList<ProviderFile>? Providers)? Options(string[] rest)
    {
        (string? Repo, bool Apply, bool Json, IReadOnlyList<ProviderFile>? Providers) options = (null, false, false, null);
        for (var i = 0; i < rest.Length; i++)
        {
            switch (rest[i])
            {
                case "--apply" when !options.Apply:
                    options.Apply = true;
                    break;
                case "--json" when !options.Json:
                    options.Json = true;
                    break;
                case "--repo" when options.Repo is null && i + 1 < rest.Length:
                    options.Repo = rest[++i];
                    break;
                case "--providers" when options.Providers is null && i + 1 < rest.Length && ParseProviders(rest[++i]) is { } providers:
                    options.Providers = providers;
                    break;
                default:
                    return null;
            }
        }

        return options;
    }
}
