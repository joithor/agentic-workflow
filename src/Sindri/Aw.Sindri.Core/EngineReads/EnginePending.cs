namespace Aw.Sindri.Core.EngineReads;

/// <summary>What the engine's instances wait on (API spec §3.8, E-30).</summary>
/// <param name="Updates">Instances waiting on an update.</param>
/// <param name="Signals">Instances waiting on a signal.</param>
/// <param name="Stuck">Instances stuck (<see cref="EngineStuck"/>).</param>
/// <param name="ContinueAsNew">Instances about to continue as new.</param>
public sealed record EnginePending(int Updates, int Signals, int Stuck, int ContinueAsNew);
