using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Features.Launching;

/// <summary>The tmux session a launch runs in: 'snd-&lt;launch id&gt;', as the launcher names it (API P10a addendum, 2026-10-16).</summary>
internal static class SessionNames
{
    /// <summary>The session name of <paramref name="launch"/>.</summary>
    /// <param name="launch">The launch.</param>
    /// <returns>The name.</returns>
    public static string For(LaunchId launch) => "snd-" + launch.Value;
}
