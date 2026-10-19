namespace Aw.Sindri.Ui.Api;

/// <summary>Why an Open would fail now.</summary>
/// <param name="Code">The SND-LAUNCH code.</param>
/// <param name="Detail">The fix.</param>
internal sealed record LaunchBlockerView(string Code, string Detail);
