namespace Aw.Sindri.Core.Workflow.Shipped;

/// <summary>One file of a recipe set (setup-once spec §5.3): a flat <c language="text">*.yaml</c> recipe, or an eval case <c language="text">evals/&lt;recipe&gt;/&lt;case&gt;.jsonl</c>.</summary>
/// <param name="RelativePath">The path relative to <c language="text">recipes/sindri</c>, with '/' separators and no leading slash.</param>
/// <param name="Bytes">The file's bytes.</param>
public sealed record RecipeSetFile(string RelativePath, ReadOnlyMemory<byte> Bytes);
