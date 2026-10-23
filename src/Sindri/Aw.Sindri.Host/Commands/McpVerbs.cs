using Aw.Context.Core;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Mcp;
using Aw.Sindri.Features.RegisterMcp;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri mcp register [--repo NAME] [--providers claude,codex,cursor|all] [--apply] [--json]' (context spec §3, API spec
/// §8): prints each provider's per-repo registration of /sindri/mcp (Claude Code's local scope, the checkout's
/// .cursor/mcp.json and .codex/config.toml; Claude Code alone without --providers); --apply writes them (repo-local, for whoever runs the
/// CLI). Local: it needs no running process.
/// </summary>
/// <param name="services">The CLI's scoped services.</param>
/// <param name="output">Where answers go.</param>
internal sealed class McpVerbs(IServiceProvider services, Output output)
{
    private const string Usage = "mcp register [--repo NAME] [--providers claude,codex,cursor|all] [--apply] [--json]";

    internal async Task<int> Run(string[] args, CancellationToken ct)
    {
        if (args is not ["register", .. var rest] || Options(rest) is not { } options)
        {
            return await output.Error(SindriErrors.UnknownVerb.With($"'mcp {string.Join(' ', args)}' is not an mcp command: {Usage}")).ConfigureAwait(false);
        }

        var answer = await services.GetRequiredService<IService<RegisterMcpRequest, RegisterMcpResponse>>()
            .Handle(new RegisterMcpRequest(Environment.CurrentDirectory, options.Repo, options.Apply, options.Providers), ct).ConfigureAwait(false);
        if (answer.IsError)
        {
            return answer.Error.Code == ContextErrors.McpRegisterRepoUnknown.Code
                ? await Refused(answer.Error).ConfigureAwait(false)
                : await output.Error(answer.Error).ConfigureAwait(false);
        }

        return await Answer(answer.Value, options.Json).ConfigureAwait(false);
    }

    /// <summary>The registration as JSON or text, then (text only) one error line per failed provider; exit 2 when any failed.</summary>
    private async Task<int> Answer(RegisterMcpResponse registration, bool json)
    {
        var failed = registration.Providers.Where(p => p.Outcome == ProviderOutcome.Failed).ToList();
        var code = failed.Count == 0 ? 0 : 2;
        if (json)
        {
            return await output.Json(registration, code).ConfigureAwait(false);
        }

        var exit = await output.Text(Text(registration), code).ConfigureAwait(false);
        foreach (var provider in failed)
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
        ProviderFile.Cursor => "cursor",
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Not a provider"),
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
                ProviderOutcome.Registered => registration.Applied ? "registered" : "would register",
                _ => throw new ArgumentOutOfRangeException(nameof(registration), provider.Outcome, "Not a provider outcome"),
            };
            text.Append("  ").Append(Name(provider.Provider)).Append(": ").Append(outcome).Append(" - ").Append(provider.Detail).Append('\n');
            if (provider.Fix is { } fix && provider.Outcome != ProviderOutcome.Failed)
            {
                text.Append("    fix: ").Append(fix).Append('\n');
            }
        }

        return text + (registration.Applied ? string.Empty : "A human at a terminal can write them with: sindri mcp register --apply\n");
    }

    private static List<ProviderFile>? ParseProviders(string value)
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

    /// <summary>The register options; null for an unknown option, a missing value or an option given twice.</summary>
    private static (string? Repo, bool Apply, bool Json, IReadOnlyList<ProviderFile>? Providers)? Options(string[] rest)
    {
        string? repo = null;
        var apply = false;
        var json = false;
        List<ProviderFile>? providers = null;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < rest.Length; i++)
        {
            if (!seen.Add(rest[i]))
            {
                return null;
            }

            switch (rest[i])
            {
                case "--apply":
                    apply = true;
                    break;
                case "--json":
                    json = true;
                    break;
                case "--repo" when i + 1 < rest.Length:
                    repo = rest[++i];
                    break;
                case "--providers" when i + 1 < rest.Length && ParseProviders(rest[++i]) is { } parsed:
                    providers = parsed;
                    break;
                default:
                    return null;
            }
        }

        return (repo, apply, json, providers);
    }
}
