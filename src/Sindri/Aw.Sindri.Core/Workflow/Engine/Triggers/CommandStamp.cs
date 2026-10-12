namespace Aw.Sindri.Core.Workflow.Engine.Triggers;

/// <summary>
/// What makes a command a decision of its own (spec §5.2): its nonce, which is the Update's id, and the SHA-256 of its
/// arguments. Its key part is 'cmd:{nonce}:{verb}:{sha256}'; a different command with a used nonce fails the API's nonce
/// check (SND-WF-009) before any Update is sent.
/// </summary>
/// <param name="Nonce">The nonce minted for the instance.</param>
/// <param name="ArgumentsSha256">The SHA-256 of the command's canonical arguments, 64 lower-case hex digits.</param>
public sealed record CommandStamp(CommandNonce Nonce, string ArgumentsSha256)
{
    /// <summary>The key part for <paramref name="verb"/>.</summary>
    /// <param name="verb">The command's verb ('answer', 'cancel', a recipe verb …).</param>
    /// <returns>'cmd:{nonce}:{verb}:{sha256}'.</returns>
    public string KeyPart(string verb) => "cmd:" + Nonce.Value + ":" + verb + ":" + ArgumentsSha256;
}
