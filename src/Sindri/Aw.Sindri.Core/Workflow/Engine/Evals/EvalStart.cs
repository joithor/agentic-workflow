using System.Text.Json;
using System.Text.Json.Serialization;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Evals;

/// <summary>An eval case's start (spec §9.2): the instance's input facts.</summary>
/// <param name="Inputs">The input facts and their content.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EvalStart(IReadOnlyDictionary<FactKey, JsonElement> Inputs);
