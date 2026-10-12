namespace Aw.Sindri.Features.StopProcess;

/// <summary>What was released.</summary>
/// <param name="Pid">The stopped process.</param>
/// <param name="Epoch">The released epoch (0 when it held none).</param>
/// <param name="Sequence">The 'process.stopped' event's ledger sequence, or null when it held no lease (nothing was recorded).</param>
public sealed record StopProcessResponse(int Pid, long Epoch, long? Sequence);
