using System.Text;
using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>
/// The one way the app reports an open desk run (the action bar's Approve, Send back and Drop): the run is read from the instance, and a
/// report is sent once and never retried by the app. A press while a report for the same instance is in flight does nothing; a refusal
/// leaves it pressable (the user may press again).
/// </summary>
internal sealed class DeskReporter(ISindriApi api, Func<Uri?> apiUrl, ILauncher? launcher = null)
{
    private const string AnswersKey = "fact:answers";
    private readonly HashSet<string> _inFlight = new(StringComparer.Ordinal);

    /// <summary>Reads the instance's open desk run.</summary>
    /// <param name="instanceId">The instance.</param>
    /// <returns>The run, or null when the instance can't be read or has no open desk run.</returns>
    internal async Task<DeskTarget?> Load(string instanceId)
    {
        if (apiUrl() is not { } url)
        {
            return null;
        }

        var read = await api.Instance(url, instanceId, CancellationToken.None).ConfigureAwait(true);
        if (read.IsError || read.Value.Current.Desk is not { } desk)
        {
            return null;
        }

        var complete = read.Value.Current.Status.Commands.FirstOrDefault(c => c.Verb == StatusVerb.Complete);
        var recipe = await api.Recipe(url, read.Value.Recipe.Id, hash: null, CancellationToken.None).ConfigureAwait(true);
        var state = recipe.IsSuccess ? recipe.Value.States.FirstOrDefault(s => string.Equals(s.State, read.Value.Current.State, StringComparison.Ordinal)) : null;
        var card = read.Value.Facts.Any(f => string.Equals(f.Key, PrStatusReader.Key, StringComparison.Ordinal)) ? await MergeCard(url, instanceId).ConfigureAwait(true) : null;
        var questions = AsksQuestions(desk, read.Value.Current.Status.Inputs) ? await Questions(url, instanceId).ConfigureAwait(true) : null;
        return new DeskTarget(instanceId, desk.Run, complete, state?.Why, OutcomeText(state, "approve"), OutcomeText(state, "revise"), desk.Recommended, card, questions);
    }

    // The ask step: the person's answers are the desk's required output and the questions are the escalation fact it was given.
    private static bool AsksQuestions(DeskForm desk, IReadOnlyList<StatusInput> inputs) =>
        desk.Outputs.Any(o => string.Equals(o.Key, AnswersKey, StringComparison.Ordinal)) && inputs.Any(i => string.Equals(i.Key, DeskQuestionReader.Key, StringComparison.Ordinal));

    // The whole list comes from the fact read: the instance's fact row only carries a one-line summary.
    private async Task<IReadOnlyList<DeskQuestion>> Questions(Uri url, string instanceId)
    {
        var fact = await api.InstanceFact(url, instanceId, DeskQuestionReader.Key, CancellationToken.None).ConfigureAwait(true);
        return fact.IsError ? [] : DeskQuestionReader.Of(fact.Value);
    }

    /// <summary>The fact "answers" for a list of answered questions: [{id, question, answer}] in the order asked.</summary>
    /// <param name="answered">The questions with their answers.</param>
    /// <returns>The JSON text.</returns>
    internal static string AnswersJson(IEnumerable<(string Id, string Question, string Answer)> answered)
    {
        ArgumentNullException.ThrowIfNull(answered);
        using var stream = new MemoryStream();
        using (var writer = new System.Text.Json.Utf8JsonWriter(stream))
        {
            writer.WriteStartArray();
            foreach (var (id, question, answer) in answered)
            {
                writer.WriteStartObject();
                writer.WriteString("id", id);
                writer.WriteString("question", question);
                writer.WriteString("answer", answer);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    // The merge stop's pull request, when the instance lists it and it reads; a fact that cannot be read leaves the card out, never the request.
    private async Task<MergeCardViewModel?> MergeCard(Uri url, string instanceId)
    {
        var fact = await api.InstanceFact(url, instanceId, PrStatusReader.Key, CancellationToken.None).ConfigureAwait(true);
        if (fact.IsError)
        {
            return null;
        }

        var status = PrStatusReader.Of(fact.Value);
        return status.IsSuccess ? new MergeCardViewModel(status.Value, launcher) : null;
    }

    // What the state says an outcome does: the outcome named exactly, or (for send back) the first one that starts with the word, as the recipe words it.
    private static string? OutcomeText(RecipeStateView? state, string outcome) =>
        state?.OnOutcome?.FirstOrDefault(o => o.Outcome.StartsWith(outcome, StringComparison.OrdinalIgnoreCase) && o.Text.Length > 0)?.Text;

    /// <summary>True when a report for the instance is in flight.</summary>
    /// <param name="instanceId">The instance.</param>
    /// <returns>True while it is.</returns>
    internal bool IsInFlight(string instanceId) => _inFlight.Contains(instanceId);

    /// <summary>Sends the report; the guard is read and set before the first await.</summary>
    /// <param name="target">The open run.</param>
    /// <param name="outcome">The outcome (approve, revise, drop).</param>
    /// <param name="answers">The text sent as the fact "answers" (a send-back's reason); null for none.</param>
    /// <returns>The answer, or null when a report for the instance was in flight or no endpoint is known.</returns>
    internal async Task<Result<CompleteStepAccepted>?> Complete(DeskTarget target, string outcome, string? answers)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (apiUrl() is not { } url || !_inFlight.Add(target.InstanceId))
        {
            return null;
        }

        try
        {
            var outputs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            if (answers is { Length: > 0 })
            {
                outputs[AnswersKey] = Encoding.UTF8.GetBytes(answers);
            }

            return await api.CompleteStep(url, target.InstanceId, new CompleteStepBody(target.InstanceId, target.Run, outcome, outputs), CancellationToken.None).ConfigureAwait(true);
        }
        finally
        {
            _ = _inFlight.Remove(target.InstanceId);
        }
    }
}
