using Aw.Context.Core;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Features.RegisterMcp;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri mcp register [--repo NAME] [--apply] [--json]' (context spec §3, API spec §8): prints the per-repo Claude Code
/// registration of /sindri/mcp; --apply runs it, for a human at a terminal only (it writes the human's own provider
/// config). Local: it needs no running process. Codex and Cursor are never registered.
/// </summary>
/// <param name="services">The CLI's scoped services.</param>
/// <param name="output">Where answers go.</param>
/// <param name="session">Who runs the CLI.</param>
internal sealed class McpVerbs(IServiceProvider services, Output output, AgentSession session)
{
    private const string Usage = "mcp register [--repo NAME] [--apply] [--json]";

    internal async Task<int> Run(string[] args, CancellationToken ct)
    {
        if (args is not ["register", .. var rest] || Options(rest) is not { } options)
        {
            return await output.Error(SindriErrors.UnknownVerb.With($"'mcp {string.Join(' ', args)}' is not an mcp command: {Usage}")).ConfigureAwait(false);
        }

        if (options.Apply && !session.Human)
        {
            return await Refused(SindriErrors.AuthRefused.With(
                $"'mcp register --apply' writes your Claude Code config, so only a human at a terminal runs it (this CLI ran as a session: {session.SessionReason}; fix: run 'sindri mcp register' and give the human the printed command)")).ConfigureAwait(false);
        }

        var answer = await services.GetRequiredService<IService<RegisterMcpRequest, RegisterMcpResponse>>()
            .Handle(new RegisterMcpRequest(Environment.CurrentDirectory, options.Repo, options.Apply), ct).ConfigureAwait(false);
        if (answer.IsError)
        {
            return answer.Error.Code == ContextErrors.McpRegisterRepoUnknown.Code
                ? await Refused(answer.Error).ConfigureAwait(false)
                : await output.Error(answer.Error).ConfigureAwait(false);
        }

        return options.Json ? await output.Json(answer.Value, 0).ConfigureAwait(false) : await output.Text(Text(answer.Value), 0).ConfigureAwait(false);
    }

    /// <summary>A refused input (spec §8.6): exit 1, attention, rather than a tool error.</summary>
    private async Task<int> Refused(Kernel.Error error)
    {
        _ = await output.Error(error).ConfigureAwait(false);
        return 1;
    }

    /// <summary>The command as one shell line, each argument quoted where it needs it.</summary>
    internal static string Line(IReadOnlyList<string> arguments) =>
        "claude " + string.Join(' ', arguments.Select(a => a.Any(c => char.IsWhiteSpace(c) || c is '\'' or '"' or '$' or '\\' or '`') ? "'" + a.Replace("'", "'\\''", StringComparison.Ordinal) + "'" : a));

    private static string Text(RegisterMcpResponse registration) =>
        (registration.Applied ? "registered (Claude Code, local scope):\n  " : "run this in the repo's main checkout (Claude Code, local scope):\n  ")
        + Line(registration.Arguments) + "\n"
        + $"  in {registration.Checkout}\n"
        + "Codex and Cursor are never registered: Sindri passes them the endpoint when it launches them (API spec §8.1).\n"
        + (registration.Applied ? string.Empty : "A human at a terminal can run it with: sindri mcp register --apply\n");

    private static (string? Repo, bool Apply, bool Json)? Options(string[] rest)
    {
        (string? Repo, bool Apply, bool Json) options = (null, false, false);
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
                default:
                    return null;
            }
        }

        return options;
    }
}
