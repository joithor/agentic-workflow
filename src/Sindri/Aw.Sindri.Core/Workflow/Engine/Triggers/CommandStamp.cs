namespace Aw.Sindri.Core.Workflow.Engine.Triggers;

/// <summary>
/// What makes a command a decision of its own (spec §5.3 step 2): its nonce and the SHA-256 of its arguments. Its key
/// part is 'cmd:{nonce}:{verb}:{sha256}', so a different command with a used nonce fails the nonce check instead.
/// </summary>
/// <param name="Nonce">The nonce minted for the instance's current version.</param>
/// <param name="ArgumentsSha256">The SHA-256 of the command's canonical arguments, 64 lower-case hex digits.</param>
public sealed record CommandStamp(CommandNonce Nonce, string ArgumentsSha256)
{
    /// <summary>The key part for <paramref name="verb"/>.</summary>
    /// <param name="verb">The command's verb ('answer', 'cancel', a recipe verb …).</param>
    /// <returns>'cmd:{nonce}:{verb}:{sha256}'.</returns>
    public string KeyPart(string verb) => "cmd:" + Nonce.Value + ":" + verb + ":" + ArgumentsSha256;
}
