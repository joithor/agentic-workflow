using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>The facts a recipe declares 'payload' (spec §3.2): JSON a later Step reads as a structure, so an agent that writes one must write a JSON object or array.</summary>
public static class PayloadFacts
{
    /// <summary>The facts <paramref name="recipe"/> declares with type 'payload', in its inputs and outputs.</summary>
    /// <param name="recipe">The recipe.</param>
    /// <returns>The facts.</returns>
    public static IReadOnlySet<FactKey> DeclaredIn(Recipe recipe) => throw new NotSupportedException("red");

    /// <summary>Why <paramref name="content"/> can't be the payload <paramref name="fact"/>, in words an agent can fix; null when it is a JSON object or array.</summary>
    /// <param name="fact">The fact.</param>
    /// <param name="content">What was written.</param>
    /// <returns>The refusal text, or null.</returns>
    public static string? Violation(FactKey fact, ReadOnlySpan<byte> content) => throw new NotSupportedException("red");

    /// <summary>The brief line that states <paramref name="fact"/>'s shape.</summary>
    /// <param name="fact">The fact.</param>
    /// <returns>The line.</returns>
    public static string Instruction(FactKey fact) => throw new NotSupportedException("red");

    /// <summary>The payload facts <paramref name="descriptor"/>'s Step writes under <paramref name="recipe"/>: its declared outputs the recipe types 'payload', in the descriptor's order.</summary>
    /// <param name="recipe">The recipe.</param>
    /// <param name="descriptor">The Step's descriptor.</param>
    /// <returns>The facts.</returns>
    public static IReadOnlyList<FactKey> OutputsOf(Recipe recipe, BlockDescriptor descriptor) => throw new NotSupportedException("red");

    /// <summary>The Step's instruction lines with the shape of each payload output added after them.</summary>
    /// <param name="instructions">What the Step is told; null for none.</param>
    /// <param name="payload">The payload facts it writes.</param>
    /// <returns>The lines; null when there are none.</returns>
    public static IReadOnlyList<string>? WithShapes(IReadOnlyList<string>? instructions, IReadOnlyList<FactKey> payload) => throw new NotSupportedException("red");
}
