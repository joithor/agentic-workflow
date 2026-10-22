namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>The 'exec-template' viewer's settings from launcher.json's 'viewer' section.</summary>
/// <param name="Name">The label the UI shows ("Sent to kitty").</param>
/// <param name="Argv">The template: closed placeholders, '{cmd...}' spliced as the attach argv.</param>
internal sealed record ExecTemplateOptions(string Name, IReadOnlyList<string> Argv)
{
    /// <summary>Variables added to the viewer process (tests point it at a private tmux directory); TMUX is always removed.</summary>
    public IReadOnlyDictionary<string, string>? Environment { get; init; }

    /// <summary>Reads an environment variable of this process (the GUI-session check); the real environment when null.</summary>
    public Func<string, string?>? GetEnv { get; init; }

    /// <summary>True on macOS, which always has a GUI session; this OS when not set (tests pin it).</summary>
    public bool IsMac { get; init; } = OperatingSystem.IsMacOS();

    /// <summary>Answers whether a program resolves on the login path; <c lang="text">ViewerProcess.OnLoginPath</c> (a real login shell) when null.</summary>
    public Func<string, CancellationToken, ValueTask<bool>>? OnPath { get; init; }
}
