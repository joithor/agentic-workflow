using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>A child's start and the recipe it starts, the one resolved and checked against the parent's pin (spec §4.1, §5.7).</summary>
/// <param name="Request">The child's start.</param>
/// <param name="Recipe">The child recipe whose hash the parent pinned.</param>
internal sealed record ChildStart(StartRequest Request, Recipe Recipe);
