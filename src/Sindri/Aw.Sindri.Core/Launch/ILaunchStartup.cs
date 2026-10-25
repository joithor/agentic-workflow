namespace Aw.Sindri.Core.Launch;

/// <summary>What the launcher service does once when it starts, before its first tick (UI spec 6.3): removes what an earlier run left behind outside the launcher's own directories.</summary>
public interface ILaunchStartup
{
    /// <summary>Removes the stale launch files of the configured terminal viewers; never throws for a file it can't delete.</summary>
    public void Sweep();
}
