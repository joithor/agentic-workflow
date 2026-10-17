using Aw.Kernel;

namespace Aw.Sindri.Core.Launch;

/// <summary>What <see cref="ILauncher.Launch"/> did: the session started (<see cref="Started"/>), or it did not (<see cref="Failed"/>).</summary>
public abstract record LaunchResult
{
    private protected LaunchResult()
    {
    }

    /// <summary>The session exists. A viewer that failed is still a start: the session keeps running (launcher SL5).</summary>
    /// <param name="Host">The session in its host.</param>
    /// <param name="Viewer">What the viewer did.</param>
    /// <param name="Attach">The command that attaches to the session (shown when the viewer didn't open).</param>
    /// <param name="CatalogSource">Where the launched entry came from.</param>
    /// <param name="ArgvHash">The hash of the entry's argv template, as the profile's stepArgvHash holds it.</param>
    public sealed record Started(HostRef Host, ViewerOutcome Viewer, IReadOnlyList<string> Attach, CatalogSource CatalogSource, string ArgvHash) : LaunchResult;

    /// <summary>The session did not start.</summary>
    /// <param name="Error">Why: a catalogue code (SND-LAUNCH-…).</param>
    /// <param name="Stage">Where it stopped.</param>
    public sealed record Failed(Error Error, LaunchStage Stage) : LaunchResult;
}
