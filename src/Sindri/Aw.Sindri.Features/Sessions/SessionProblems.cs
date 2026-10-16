using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Features.Sessions;

/// <summary>The problem rows of a pass: a fixed sentence per code, never an exception message (collector spec §4.1, §9).</summary>
internal static class SessionProblems
{
    internal static SessionProblem ProcessTable(Error error) => error.Message switch
    {
        "unsupported-platform" => new("processes", SindriErrors.SessionsUnavailable.Code, "Sindri can't see agent sessions on this system: unsupported-platform"),
        _ => new("processes", SindriErrors.SessionsUnavailable.Code, "Sindri can't see agent sessions on this system: proc-unreadable"),
    };

    internal static SessionProblem Disabled { get; } = new("collector", SindriErrors.SessionsUnavailable.Code, "Sindri can't see agent sessions on this system: disabled");

    internal static SessionProblem TestOverride { get; } = new("collector", SindriErrors.SessionsTestOverride.Code, "A test override is set in this run: the sessions shown may be fixtures");

    internal static SessionProblem Launcher { get; } = new("launcher.json", SindriErrors.LauncherInvalid.Code, "launcher.json isn't valid; the built-in agents and default settings are used");

    internal static SessionProblem Source(string source) => new(source, SindriErrors.SessionSourceFailed.Code, $"The {source} source couldn't be read in the last pass");

    internal static SessionProblem Overrun { get; } = new("collector", SindriErrors.SessionSourceFailed.Code, "A pass took longer than its 500 ms budget");
}
