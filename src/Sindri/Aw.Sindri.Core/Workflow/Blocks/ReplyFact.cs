namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>
/// A fact an effect's reply carries (<see cref="EffectDone.Facts"/>), as the effect stored it: this namespace stays below the engine's, so the engine's
/// 'FactValue' is not named here; the 'RunEffect' activity turns it into one.
/// </summary>
/// <param name="Ref">Where the content is stored (a blob reference the fact store resolves).</param>
/// <param name="Sha256">The content's SHA-256, 64 lower-case hex digits.</param>
/// <param name="HeadSha">The git head it was recorded at, when the fact belongs to a repo.</param>
public sealed record ReplyFact(string Ref, string Sha256, string? HeadSha);
