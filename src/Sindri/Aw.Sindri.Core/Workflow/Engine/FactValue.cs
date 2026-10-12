namespace Aw.Sindri.Core.Workflow.Engine;

/// <summary>
/// A fact as an instance holds it (spec §5.1): a reference to the stored content, its SHA-256, and the git head it
/// was recorded at. The content itself lives outside the instance row; no-progress compares digests (A §17.23.13).
/// </summary>
/// <param name="Ref">Where the content is stored (a blob reference the fact store resolves).</param>
/// <param name="Sha256">The content's SHA-256, 64 lower-case hex digits.</param>
/// <param name="HeadSha">The git head it was recorded at, when the fact belongs to a repo.</param>
public sealed record FactValue(string Ref, string Sha256, string? HeadSha);
