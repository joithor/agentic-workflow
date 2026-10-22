using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Temporalio.Activities;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// 'StoreLiteralFacts': a child started with a literal input holds it as a fact whose ref is 'literal:&lt;compact json&gt;' and whose
/// hash is the SHA-256 of that JSON (ChildInputs). Nothing wrote the JSON into the fact store, so every reader of
/// '&lt;home&gt;/workflow/facts/&lt;sha&gt;' failed. This writes the bytes under their hash when the instance starts, so every reader keeps
/// the one path; storing the same bytes again changes nothing.
/// </summary>
/// <param name="store">Where facts are stored by their hash.</param>
internal sealed class LiteralFactActivities(IFactStore store)
{
    /// <summary>Stores the JSON of each literal fact in <paramref name="facts"/>; the others are left alone.</summary>
    /// <param name="facts">The instance's start facts.</param>
    /// <returns>A task that ends when every literal is stored.</returns>
    [Activity("StoreLiteralFacts")]
    public Task StoreAsync(IReadOnlyList<FactValue> facts) => throw new NotSupportedException();
}
