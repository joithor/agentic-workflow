using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>What a child's end carries to its parent (DT spec §8.4): its declared outputs, and which of them are present per outcome.</summary>
#pragma warning disable MA0182 // why: the red stub; the wiring rules use it from the next commit
internal static class ChildOutputs
{
    public static IReadOnlyList<FactKey> Declared(Recipe child) => throw new NotSupportedException();

    public static IReadOnlyDictionary<OutcomeName, IReadOnlySet<FactKey>> PresentOn(Recipe child) => throw new NotSupportedException();
}
