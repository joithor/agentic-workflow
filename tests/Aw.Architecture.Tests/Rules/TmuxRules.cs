using System.Diagnostics;

namespace Aw.Architecture.Tests.Rules;

/// <summary>
/// Only one type in Sindri's session namespaces runs tmux, and only through five read-only verbs (collector spec CI-5). A second type may build the attach
/// command as data for a copy line or a viewer, and nothing that starts a process may take that line. 'Builds a tmux argv' is read from the IL: a type that
/// loads a tmux verb as a string literal, or starts a process and names the 'tmux' executable.
/// </summary>
public static class TmuxRules
{
    /// <summary>A Sessions type that builds or runs tmux outside the two allowed ones, or an allowed one that strays from its contract.</summary>
    public const string Id = "ARCH-SESS-001";

    /// <summary>The verbs Sindri runs, and the only ones: the one named list; a new verb is a reviewed change here, in TmuxVerb and in TmuxCli.</summary>
    public static IReadOnlyList<string> ReadVerbs { get; } = ["list-sessions", "list-panes", "list-clients", "display-message", "capture-pane"];

    private const string Executable = "tmux";
    private const string SocketFlag = "-S";

    /// <summary>The verbs that would change a tmux server or reach into a pane, with the attach verbs: a literal of one anywhere in a Sessions type is a finding unless the type is the attach line.</summary>
    private static readonly string[] _otherVerbs =
    [
        "attach", "attach-session", "new", "new-session", "new-window", "split-window", "send-keys", "send-prefix", "kill-server", "kill-session", "kill-window",
        "kill-pane", "switch-client", "select-pane", "select-window", "run-shell", "respawn-pane", "respawn-window", "set-option", "set-environment", "source-file",
        "pipe-pane", "paste-buffer", "load-buffer", "save-buffer", "rename-session", "detach-client", "refresh-client", "if-shell", "display-popup",
    ];

    /// <summary>Checks the types of Sindri's Sessions namespaces.</summary>
    /// <param name="types">Every type of the Sessions namespaces (nested ones are read through their owners).</param>
    /// <param name="cliName">The one type that runs tmux.</param>
    /// <param name="attachName">The one type that builds the attach command as data.</param>
    /// <returns>One violation per finding.</returns>
    public static IEnumerable<Violation> Check(IEnumerable<Type> types, string cliName, string attachName)
    {
        List<Type> owners = [.. types.Where(t => !t.IsNested)];
        var cli = owners.Find(t => t.Name == cliName);
        var attach = owners.Find(t => t.Name == attachName);
        var violations = new List<Violation>();
        if (cli is null)
        {
            violations.Add(new Violation(Id, $"no {cliName} type in the Sessions namespaces", $"add {cliName}: the one type that runs tmux, through the five read verbs"));
        }

        if (attach is null)
        {
            violations.Add(new Violation(Id, $"no {attachName} type in the Sessions namespaces", $"add {attachName}: it builds the attach command as data and nothing executes it"));
        }

        violations.AddRange(owners.SelectMany(t => Scan(t, cli, attach)));
        if (cli is not null)
        {
            violations.AddRange(CliContract(cli));
        }

        if (attach is not null && IlScan.CalledTypes(attach).Any(static c => c == typeof(Process) || c == typeof(ProcessStartInfo)))
        {
            violations.Add(new Violation(Id, $"{attach.FullName} starts a process", "it only builds the command; the human runs the copy line and a viewer's terminal runs it"));
        }

        return violations;
    }

    private static IEnumerable<Violation> Scan(Type type, Type? cli, Type? attach)
    {
        var literals = IlScan.Literals(type);
        var called = IlScan.CalledTypes(type);
        var startsProcess = called.Any(static c => c == typeof(Process) || c == typeof(ProcessStartInfo));
        if (type != cli && type != attach)
        {
            foreach (var stray in Stray(type, literals, startsProcess))
            {
                yield return stray;
            }
        }

        if (startsProcess && attach is not null && called.Contains(attach))
        {
            yield return new Violation(Id, $"{type.FullName} starts a process and calls {attach.Name}", "the attach command is data for the human or a viewer: pass it to a port, never to a process runner");
        }
    }

    private static IEnumerable<Violation> Stray(Type type, IReadOnlySet<string> literals, bool startsProcess)
    {
        foreach (var verb in literals.Where(l => ReadVerbs.Contains(l, StringComparer.Ordinal) || _otherVerbs.Contains(l, StringComparer.Ordinal)).Order(StringComparer.Ordinal))
        {
            yield return new Violation(Id, $"{type.FullName} names the tmux verb '{verb}'", "build tmux commands only in TmuxCli (five read verbs) and TmuxAttachLine (the attach line as data)");
        }

        if (startsProcess && literals.Contains(Executable))
        {
            yield return new Violation(Id, $"{type.FullName} starts a process and names 'tmux'", "run tmux only through TmuxCli");
        }
    }

    private static IEnumerable<Violation> CliContract(Type cli)
    {
        var literals = IlScan.Literals(cli);
        foreach (var other in literals.Where(l => _otherVerbs.Contains(l, StringComparer.Ordinal) && !ReadVerbs.Contains(l, StringComparer.Ordinal)).Order(StringComparer.Ordinal))
        {
            yield return new Violation(Id, $"{cli.FullName} names the tmux verb '{other}'", $"its verbs are exactly {string.Join(", ", ReadVerbs)}");
        }

        foreach (var missing in ReadVerbs.Where(v => !literals.Contains(v)))
        {
            yield return new Violation(Id, $"{cli.FullName} never names the verb '{missing}'", $"its verbs are exactly {string.Join(", ", ReadVerbs)}");
        }

        if (!literals.Contains(SocketFlag))
        {
            yield return new Violation(Id, $"{cli.FullName} never passes {SocketFlag}", "every call names its socket explicitly with -S");
        }
    }
}
