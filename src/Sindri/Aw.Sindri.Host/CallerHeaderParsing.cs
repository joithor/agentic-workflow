using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Host;

/// <summary>
/// Reads the caller headers (API spec §6, plan G12): pure, so every case is a unit test. A missing X-Sindri-Caller or
/// 'session' is a session (the safe default), 'human' is a human, and any other value is SND-API-007. The terminal claim
/// counts only with human; an unknown client label counts as none; a malformed repo name is SND-API-007.
/// </summary>
internal static class CallerHeaderParsing
{
    /// <summary>Parses the four caller header values (each null when the header is missing).</summary>
    internal static Result<ParsedCaller> Parse(string? caller, string? terminal, string? client, string? repo)
    {
        CallerClass? callerClass = caller switch
        {
            null or "session" => CallerClass.Session,
            "human" => CallerClass.Human,
            _ => null,
        };
        if (callerClass is not { } known)
        {
            return SindriErrors.CallerUnknown.With($"X-Sindri-Caller '{caller}' isn't 'human' or 'session' (fix: send one of them, or leave the header out to call as a session)");
        }

        RepoName? scope = null;
        if (repo is not null)
        {
            if (!RepoName.TryFrom(repo, out var name))
            {
                return SindriErrors.CallerUnknown.With($"X-Sindri-Repo '{repo}' isn't a repo name (fix: send the repo's name from the profile, or leave the header out)");
            }

            scope = name;
        }

        var label = Label(client);
        var human = known == CallerClass.Human;
        return new ParsedCaller(known, human && string.Equals(terminal, "1", StringComparison.Ordinal), label, scope,
            client is not null && label == ClientLabel.None ? client : null);
    }

    private static ClientLabel Label(string? client) => client switch
    {
        "cli" => ClientLabel.Cli,
        "ui" => ClientLabel.Ui,
        "prover" => ClientLabel.Prover,
        "mcp" => ClientLabel.Mcp,
        _ => ClientLabel.None,
    };
}
