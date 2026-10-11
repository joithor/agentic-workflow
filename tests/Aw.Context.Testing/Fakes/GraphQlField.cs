using System.Text.Json.Nodes;

namespace Aw.Context.Testing.Fakes;

/// <summary>A root field of a GraphQL query, with its arguments resolved against the request's variables.</summary>
/// <param name="ResponseKey">The alias, or the field name.</param>
/// <param name="Name">The field name.</param>
/// <param name="Arguments">Argument name to value (null for an unset variable or a literal null).</param>
internal sealed record GraphQlField(string ResponseKey, string Name, IReadOnlyDictionary<string, JsonNode?> Arguments);
