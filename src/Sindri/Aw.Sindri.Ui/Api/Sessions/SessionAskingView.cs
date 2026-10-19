namespace Aw.Sindri.Ui.Api.Sessions;

/// <summary>What a waiting session waits on.</summary>
/// <param name="Reason">Reason, as served (approval and the like).</param>
/// <param name="Tool">Tool, as served.</param>
/// <param name="Since">Since, as served.</param>
internal sealed record SessionAskingView(string Reason, string? Tool, DateTimeOffset Since);
