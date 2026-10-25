namespace Aw.Sindri.Testing.Workflow.ApproveAll;

/// <summary>How the approval checks refuse a seeded recipe: the error code, the plain sentence, and the one-recipe approval to run instead (null when the check has none).</summary>
/// <param name="Code">The error code.</param>
/// <param name="Sentence">The plain sentence.</param>
/// <param name="Cli">The CLI form, or null.</param>
public sealed record SeededRefusal(string Code, string Sentence, string? Cli);
