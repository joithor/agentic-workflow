using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

internal sealed partial class RecipeWorkflow
{
    private static readonly FactKey _recommendationFact = FactKey.From("fact:recommendation");

    private string? _recommendation;   // the stored text of fact:recommendation the stop's form reads
    private string? _recommendationSha;

    /// <summary>
    /// Reads 'fact:recommendation' once when a new one is held ('ReadSubject', a local activity: no I/O in workflow code), so the open stop's form can say which outcome the step before it recommends.
    /// A recommendation the stop's Step doesn't have the outcome for is dropped with a warning, not an error. A history with no such fact never runs the activity, so it replays as recorded.
    /// </summary>
    private async Task ReadRecommendation(WorkflowInstance after)
    {
        if (after.Facts.TryGetValue(_recommendationFact, out var fact) && !string.Equals(fact.Sha256, _recommendationSha, StringComparison.Ordinal))
        {
            _recommendationSha = fact.Sha256;
            _recommendation = await SubjectTextOf(fact).ConfigureAwait(true);
        }

        if (_recommendation is not null && _recipe is { } recipe && Recommendations.Dropped(_recommendation, after, recipe))
        {
            Dropped(Workflow.Logger, after.Address.Value, after.Frame.State);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Address}: the recommendation held when entering {State} isn't an outcome of its step (or isn't readable), so it is dropped")]
    private static partial void Dropped(ILogger logger, string address, string state);
}
