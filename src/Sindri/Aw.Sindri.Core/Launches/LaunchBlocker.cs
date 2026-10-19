namespace Aw.Sindri.Core.Launches;

/// <summary>Why an Open would fail now.</summary>
/// <param name="Code">The SND-LAUNCH code it would carry.</param>
/// <param name="Detail">The fix.</param>
public sealed record LaunchBlocker(string Code, string Detail);
