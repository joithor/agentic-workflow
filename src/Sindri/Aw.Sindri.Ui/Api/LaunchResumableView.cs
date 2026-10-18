namespace Aw.Sindri.Ui.Api;

/// <summary>A finished session that can be resumed.</summary>
/// <param name="Target">The agent id.</param>
/// <param name="SessionId">The agent's own session id.</param>
internal sealed record LaunchResumableView(string Target, string SessionId);
