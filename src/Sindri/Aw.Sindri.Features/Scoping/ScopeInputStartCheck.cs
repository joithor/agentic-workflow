using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;


namespace Aw.Sindri.Features.Scoping;

/// <summary>The start-time check of 'fact:scopeInput' against schemas/scoping/scope-input.json: one of four shapes told apart by 'kind', each naming its repo.</summary>
internal sealed class ScopeInputStartCheck : IStartFactCheck
{
    private const string Fix = "write fact:scopeInput as one of the shapes in schemas/scoping/scope-input.json (a kind of brief, tracker, idea or change, each with its repo)";

    public StartFactProblem? Check(RecipeId recipe, FactKey fact, ReadOnlyMemory<byte> content)
    {
        ArgumentNullException.ThrowIfNull(fact.Value);
        if (!string.Equals(fact.Value, "fact:scopeInput", StringComparison.Ordinal))
        {
            return null;
        }

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(content.Span);
        }
        catch (JsonException e) // boundary: a fact the human gave is input, and unreadable input is their error
        {
            return new StartFactProblem([$"isn't JSON: {e.Message}"], Fix);
        }

        var errors = ScopeSchemas.Errors("scope-input", node);
        return errors.Count == 0 ? null : new StartFactProblem([.. errors.Select(static e => $"{(e.Path.Length == 0 ? "/" : e.Path)}: {e.Message}")], Fix);
    }
}
