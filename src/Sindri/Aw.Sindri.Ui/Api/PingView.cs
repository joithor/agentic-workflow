namespace Aw.Sindri.Ui.Api;

/// <summary>GET /sindri/ping.</summary>
/// <param name="Pid">The answering process.</param>
/// <param name="Epoch">The lease epoch it holds (0 when none).</param>
internal sealed record PingView(int Pid, long Epoch);
