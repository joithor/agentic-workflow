namespace Aw.Sindri.Core.EngineReads;

/// <summary>'GET /sindri/engine' (API spec §3.8, E-30): the server, the worker, the mirror and what is pending.</summary>
/// <param name="Server">The Temporal server.</param>
/// <param name="Worker">The worker.</param>
/// <param name="Mirror">The ledger mirror's lag.</param>
/// <param name="Pending">What the instances wait on.</param>
/// <param name="AsOf">When this was read.</param>
public sealed record EngineView(EngineServer Server, EngineWorker Worker, EngineMirror Mirror, EnginePending Pending, DateTimeOffset AsOf);
