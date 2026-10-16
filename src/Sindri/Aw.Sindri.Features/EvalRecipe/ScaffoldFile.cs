namespace Aw.Sindri.Features.EvalRecipe;

/// <summary>One scaffolded case: the file name it is written under and its JSON lines.</summary>
/// <param name="Name">The case's name; the file is '&lt;Name&gt;.jsonl'.</param>
/// <param name="Text">The case's JSON lines.</param>
public sealed record ScaffoldFile(string Name, string Text);
