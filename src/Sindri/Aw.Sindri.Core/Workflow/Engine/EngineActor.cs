namespace Aw.Sindri.Core.Workflow.Engine;

/// <summary>The engine's own actor (engine spec §5.2, §6.2): the one an 'open-request' and an instance started taken over (UI E-19) carry.</summary>
public static class EngineActor
{
    /// <summary>The actor of every trigger the engine sends itself: 'service:engine'.</summary>
    public const string Name = "service:engine";
}
