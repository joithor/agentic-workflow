using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The OS side of the dev server: finding 'temporal server start-dev' processes by their database (ps), reading their
/// ports from the command line, signalling them and running the CLI for its version. One home has one database, so the
/// database path is what identifies "our" server, whoever started it.
/// </summary>
internal static partial class DevServerProcesses
{
    private const string StartDev = " server start-dev ";

    /// <summary>The dev servers running on <paramref name="database"/>, read from 'ps'.</summary>
    /// <param name="database">The absolute path of the SQLite file.</param>
    /// <returns>The servers, oldest pid first.</returns>
    public static IReadOnlyList<RunningTemporalServer> On(string database) =>
        [.. Capture("/bin/ps", "-axww", "-o", "pid=,command=").Split('\n')
            .Select(line => Parse(line.Trim(), database))
            .OfType<RunningTemporalServer>()
            .OrderBy(s => s.ProcessId)];

    /// <summary>Reads one 'ps' line: a dev server on <paramref name="database"/> with a loopback frontend, or null.</summary>
    /// <param name="line">'&lt;pid&gt; &lt;command line&gt;'.</param>
    /// <param name="database">The database to match.</param>
    /// <returns>The server, or null when the line is something else.</returns>
    internal static RunningTemporalServer? Parse(string line, string database)
    {
        var start = line.IndexOf(StartDev, StringComparison.Ordinal);
        var space = line.IndexOf(' ', StringComparison.Ordinal);
        if (start < 0 || space < 0 || !(line + " ").Contains($" --db-filename {database} ", StringComparison.Ordinal)
            || !int.TryParse(line.AsSpan(0, space), NumberStyles.None, CultureInfo.InvariantCulture, out var pid)
            || Flag(line, PortFlag) is not { } port)
        {
            return null;
        }

        return new RunningTemporalServer(pid, new IPEndPoint(IPAddress.Loopback, port), database)
        {
            UiPort = Flag(line, UiPortFlag) ?? 0,
            Executable = line[(space + 1)..start],
        };
    }

    /// <summary>The CLI's version ('temporal version 1.9.1 (Server …)' reads as 1.9.1), or null when it can't be read.</summary>
    /// <param name="executable">The 'temporal' executable.</param>
    /// <returns>The version.</returns>
    public static string? Version(string executable)
    {
        if (!File.Exists(executable))
        {
            return null;
        }

        var match = VersionLine.Match(Capture(executable, "--version"));
        return match.Success ? match.Groups["value"].Value : null;
    }

    /// <summary>Sends <paramref name="signal"/> ('TERM', 'KILL', 'STOP', 'CONT') to <paramref name="pid"/>.</summary>
    /// <param name="pid">The process.</param>
    /// <param name="signal">The signal's name.</param>
    public static void Signal(int pid, string signal) =>
        _ = Capture("/bin/kill", "-s", signal, pid.ToString(CultureInfo.InvariantCulture));

    /// <summary>True when <paramref name="pid"/> no longer runs.</summary>
    /// <param name="pid">The process.</param>
    /// <returns>Whether it is gone.</returns>
    public static bool Gone(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return process.HasExited;
        }
        catch (ArgumentException)
        {
            return true;   // no process with that id
        }
        catch (InvalidOperationException)
        {
            return true;   // it exited while being read
        }
    }

    /// <summary>Runs <paramref name="file"/> to completion (at most 10 s) and returns its standard output.</summary>
    /// <param name="file">The executable.</param>
    /// <param name="arguments">Its arguments.</param>
    /// <returns>Its standard output.</returns>
    public static string Capture(string file, params string[] arguments)
    {
        var info = ChildEnvironment.Start(file, arguments);
        info.RedirectStandardOutput = true;
        info.RedirectStandardError = true;
        using var process = Process.Start(info) ?? throw new InvalidOperationException($"{file} didn't start");
        var output = process.StandardOutput.ReadToEndAsync();
        _ = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(10_000))
        {
            process.Kill();
            throw new TimeoutException($"{file} didn't finish within 10 s");
        }

#pragma warning disable RS0030 // the child has exited, so its stream reaches end of file at once; this short-lived tool leaves no grandchild holding the pipe
        return output.GetAwaiter().GetResult();
#pragma warning restore RS0030
    }

    private static int? Flag(string line, Regex flag) =>
        flag.Match(line) is { Success: true } m && int.TryParse(m.Groups["value"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : null;

    [GeneratedRegex(@" --port (?<value>\d+)", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PortFlag { get; }

    [GeneratedRegex(@" --ui-port (?<value>\d+)", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex UiPortFlag { get; }

    [GeneratedRegex(@"temporal version (?<value>\S+)", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex VersionLine { get; }
}
