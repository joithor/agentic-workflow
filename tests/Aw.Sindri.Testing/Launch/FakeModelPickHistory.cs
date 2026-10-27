using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Testing.Launch;

/// <summary>An <see cref="IModelPickHistory"/> in memory: answers the rows a test gave it for the models asked about.</summary>
public sealed class FakeModelPickHistory : IModelPickHistory
{
    private readonly List<ModelHistoryRow> _rows = [];

    /// <summary>The rows every call draws from.</summary>
    public IList<ModelHistoryRow> Rows => _rows;

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<ModelHistoryRow>> For(RecipeId recipe, int version, string state, IReadOnlyList<string> models, CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<ModelHistoryRow>>([.. _rows.Where(r => models.Contains(r.Model, StringComparer.Ordinal))]);
}
