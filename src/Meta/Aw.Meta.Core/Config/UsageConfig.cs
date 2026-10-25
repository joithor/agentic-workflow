namespace Aw.Meta.Core.Config;

/// <summary>When the usage-limit monitor calls a limit over and whether it types the resume line itself; every field is optional.</summary>
/// <param name="ResumeMarginSeconds">How long after a limit's stated reset time the window counts as available again.</param>
/// <param name="ProbeMinutes">How often a limit with an unknown reset time (and a resume that did not take) is tried again.</param>
/// <param name="AutoResume">Whether the watch types the resume line into a lane that sits idle at the limit's stop.</param>
public sealed record UsageConfig(int ResumeMarginSeconds = 120, int ProbeMinutes = 15, bool AutoResume = true);
