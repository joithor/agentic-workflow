namespace Aw.Sindri.Features.StopProcess;

/// <summary>Stop the control plane: record the stop, release the lease, shut the process down.</summary>
/// <param name="Reason">'stop' when a client asked, 'signal' on SIGTERM or SIGINT.</param>
public sealed record StopProcessRequest(string Reason);
