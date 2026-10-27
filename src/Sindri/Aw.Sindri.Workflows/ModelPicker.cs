namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// Picks the model an engine launch runs on (model-pick spec 4, 5): the candidates after the step's floor and the profile's pin and cap, then Jev among them, then the rules fallback.
/// The pick is stored first ('model.picking', 'model.picked' under the launch's keys), so an activity retry reads it back and never asks Jev twice.
/// </summary>
internal sealed class ModelPicker
{
    /// <summary>Picks the model for <paramref name="work"/>.</summary>
    /// <param name="work">The run and its launch request.</param>
    /// <param name="cancellationToken">Cancels the pick.</param>
    /// <returns>The model, or the refusal when no model is allowed.</returns>
    public Task<ModelPickOutcome> Pick(ModelPickWork work, CancellationToken cancellationToken) => throw new NotSupportedException();
}
