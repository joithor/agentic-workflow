namespace Aw.Sindri.Core.Workflow.Engine.Triggers;

/// <summary>A recipe command, 'command.&lt;verb&gt;' (spec §3.5, class desk): allowed from any non-terminal state that has a transition on it.</summary>
/// <param name="Actor">The caller.</param>
/// <param name="Verb">The verb.</param>
/// <param name="Stamp">The nonce and argument digest.</param>
public sealed record CommandFired(string Actor, CommandVerb Verb, CommandStamp Stamp) : Trigger(Actor)
{
    /// <inheritdoc/>
    public override string Name => TriggerNames.CommandPrefix + Verb.Value;

    /// <inheritdoc/>
    public override string KeyPart => Stamp.KeyPart(Verb.Value);
}
