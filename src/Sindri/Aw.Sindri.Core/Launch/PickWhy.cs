using Aw.Sindri.Core.Events;

namespace Aw.Sindri.Core.Launch;

/// <summary>
/// The one plain sentence for why a step got its model (model-pick spec §6.1, "Why, in plain words"). Pure: 'sindri launch picks'
/// prints it, the route serves it and the app draws it, so the words are in one place.
/// </summary>
public static class PickWhy
{
    /// <summary>The sentence while only 'model.picking' exists for a launch.</summary>
    public const string Asking = "Asking Jev.";

    /// <summary>The reason a 'default' pick carries when model choice is off (workflow.agent.pick: off).</summary>
    public const string PickOff = "pick-off";

    /// <summary>The sentence for a recorded pick.</summary>
    /// <param name="pick">The pick.</param>
    /// <returns>The sentence: the escalation's when the pick is a retry, else the one for its by and reason, with the unusual note added when the pick says so.</returns>
    public static string Sentence(ModelPicked pick) => throw new NotSupportedException("PickWhy is not built yet");
}
