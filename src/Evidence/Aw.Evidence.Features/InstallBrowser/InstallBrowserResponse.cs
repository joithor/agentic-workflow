namespace Aw.Evidence.Features.InstallBrowser;

/// <summary>The install's outcome.</summary>
/// <param name="Installed">Always true on success (an existing install counts).</param>
public sealed record InstallBrowserResponse(bool Installed);
