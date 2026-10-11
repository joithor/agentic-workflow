using Aw.Kernel.Ids;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Commands;

/// <summary>
/// Who is calling, for the command chain and every event the call records (API spec §2.1, §6). The Host fills it from
/// the request's headers; in-process callers (the host, the scheduler, the engine) fill it themselves. The actor is
/// composed here from the class and the client label, never taken from a header.
/// </summary>
/// <param name="Class">The caller class.</param>
/// <param name="Terminal">True only for a human at an interactive terminal.</param>
/// <param name="Actor">The envelope's actor: 'human:cli', 'session:mcp', 'service:host' and the like.</param>
/// <param name="CommandId">This attempt's id.</param>
/// <param name="CorrelationId">The correlation every event of this call carries.</param>
/// <param name="Repo">The repo a session caller is scoped to (X-Sindri-Repo), or null.</param>
public sealed record CallerContext(CallerClass Class, bool Terminal, string Actor, CommandId CommandId, CorrelationId CorrelationId, RepoName? Repo)
{
    /// <summary>A caller over HTTP: the actor from the class and the label; the terminal flag is honoured only for a human.</summary>
    /// <param name="caller">Human or session (service never arrives over HTTP; it is read as a session).</param>
    /// <param name="terminal">The X-Sindri-Terminal claim.</param>
    /// <param name="client">The X-Sindri-Client label.</param>
    /// <param name="repo">The X-Sindri-Repo scope, or null.</param>
    /// <param name="ids">The id port, for this attempt's command and correlation ids.</param>
    /// <returns>The context.</returns>
    public static CallerContext Http(CallerClass caller, bool terminal, ClientLabel client, RepoName? repo, IIdGenerator ids)
    {
        var human = caller == CallerClass.Human;
        var actor = human ? "human:" + HumanLabel(client) : "session:" + SessionLabel(client);
        return new CallerContext(human ? CallerClass.Human : CallerClass.Session, human && terminal, actor, ids.New<CommandId>(), ids.New<CorrelationId>(), repo);
    }

    /// <summary>An in-process caller, 'service:&lt;name&gt;'.</summary>
    /// <param name="name">The service, for example 'host' or 'heartbeat'.</param>
    /// <param name="ids">The id port, for this run's command and correlation ids.</param>
    /// <returns>The context.</returns>
    public static CallerContext Service(string name, IIdGenerator ids) =>
        new(CallerClass.Service, Terminal: false, "service:" + name, ids.New<CommandId>(), ids.New<CorrelationId>(), Repo: null);

    private static string HumanLabel(ClientLabel client) => client switch
    {
        ClientLabel.Cli => "cli",
        ClientLabel.Ui => "ui",
        ClientLabel.Prover => "prover",
        ClientLabel.None or ClientLabel.Mcp => "http",
        _ => "http",
    };

    private static string SessionLabel(ClientLabel client) => client switch
    {
        ClientLabel.Cli => "cli",
        ClientLabel.Mcp => "mcp",
        ClientLabel.None or ClientLabel.Ui or ClientLabel.Prover => "http",
        _ => "http",
    };
}
