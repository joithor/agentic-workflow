using System.Text;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Start;

/// <summary>The Start form's send (<see cref="StartFormViewModel"/>, continued): what goes on the wire, the once-only guard and the answers.</summary>
internal sealed partial class StartFormViewModel
{
    /// <summary>Sends the form once.</summary>
    /// <param name="cancellationToken">Cancels the wait for the answer.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal Task Submit(CancellationToken cancellationToken) => CanStart ? Send(next: false, cancellationToken) : Task.CompletedTask;

    /// <summary>Sends the form again as the next attempt: a second, explicit press, offered only after a finished attempt was shown.</summary>
    /// <param name="cancellationToken">Cancels the wait for the answer.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal Task StartNewAttempt(CancellationToken cancellationToken) => CanStartNewAttempt ? Send(next: true, cancellationToken) : Task.CompletedTask;

    // The guard is read and set before the first await: a press while this one is in flight does nothing.
    private async Task Send(bool next, CancellationToken cancellationToken)
    {
        ClearError();
        if (Build(next) is not { } request)
        {
            return;
        }

        if (_url() is not { } api)
        {
            Fail(SindriUiErrors.NotRunning.With("no API to send to"));
            return;
        }

        var (form, edits) = (_form, _edits);
        IsSending = true;
        Result<StartedView> answer;
        try
        {
            answer = await _api.StartInstance(api, request, cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            if (form == _form)
            {
                IsSending = false;
            }
        }

        if (form == _form)   // else the form was closed (or closed and opened again): this answer is for the one that sent it
        {
            Answered(answer, next, current: edits == _edits);
        }
    }

    // A refusal of a send made before an edit is about a form that is gone: it neither shows nor takes focus.
    private void Answered(Result<StartedView> answer, bool next, bool current)
    {
        if (!answer.IsError)
        {
            Accepted(answer.Value, next, current);
        }
        else if (current)
        {
            Fail(answer.Error);
        }
    }

    // What goes on the wire, or null with the reason shown: the recipe and the item first, then each input in the recipe's order.
    private StartInstanceRequest? Build(bool next)
    {
        if (Recipe is not { } recipe)
        {
            Refuse(Labels.StartChooseRecipe, StartErrorTarget.Recipe, field: null);
            return null;
        }

        var item = ItemId.Trim();
        if (item.Length == 0)
        {
            Refuse(Labels.StartChooseItem, StartErrorTarget.Item, field: null);
            return null;
        }

        var checks = Fields.Select(f => (f.Key, Check: f.Check())).ToList();
        if (checks.FirstOrDefault(c => c.Check.Problem.Length > 0) is { Check.Problem.Length: > 0 } wrong)
        {
            Refuse(wrong.Check.Problem, StartErrorTarget.Field, Fields.First(f => string.Equals(f.Key, wrong.Key, StringComparison.Ordinal)));   // why: the key came from Fields
            return null;
        }

        var facts = new Dictionary<string, StartFact>(StringComparer.Ordinal);
        foreach (var (key, check) in checks.Where(c => c.Check.Fact is not null))
        {
            facts[key] = check.Fact!;   // why: filtered to the facts that exist
        }

        if (_itemKey is { } itemKey)
        {
            facts[itemKey] = ItemFact(item);   // SND-WF-014: a recipe's item input must name the request's item, so it is made from the one picked
        }

        return new StartInstanceRequest(recipe.Id, item, next, facts);
    }

    // The engine's item fact is {id, repo, spec}: effect.claim.ref reads the repo from it. A picked item brings its repo; a typed id the queue does not
    // list has none, and the fact is the id alone (SND-WF-014 compares only the id).
    private StartFact ItemFact(string item)
    {
        var fact = new JsonObject { ["id"] = item };
        if (Items.FirstOrDefault(i => string.Equals(i.Id, item, StringComparison.Ordinal))?.Repo is { Length: > 0 } repo)
        {
            fact["repo"] = repo;
        }

        return new StartFact(ItemFactName, Encoding.UTF8.GetBytes(fact.ToJsonString()));
    }

    private void Accepted(StartedView started, bool next, bool current)
    {
        if (!current)
        {
            // The item or recipe was changed while this was on its way: the answer is for what was sent, so it opens its frame and says nothing about the form as it is now.
            OpenedFrame = true;
            Say(Labels.StartedBehind(started.Instance.Address));
            Started?.Invoke(this, new StartedEventArgs(started.Instance.Id, started.Instance.Address, started.Existing, started.Finished));
            return;
        }

        if (next)
        {
            _sentNext = true;
        }
        else
        {
            _sentFirst = true;
        }

        var instance = started.Instance;
        OpenedFrame = true;   // its frame opens, behind the form while the form stays (an existing attempt), else once it has closed
        OffersNewAttempt = false;
        Outcome = !started.Existing ? string.Empty : started.Finished ? Labels.StartFinished(instance.Address) : Labels.StartAlreadyRunning(instance.Address);
        OffersNewAttempt = started.Existing && started.Finished && !_sentNext;
        OnChanged(nameof(CanStart));
        Started?.Invoke(this, new StartedEventArgs(instance.Id, instance.Address, started.Existing, started.Finished));
        if (!started.Existing)
        {
            Close();   // a new instance: its frame is open, there is nothing more to say here
        }
        else
        {
            OutcomeFocusRequests++;   // the Start button that was pressed is off now: focus goes to the new-attempt button or the outcome line
        }
    }

    private void Fail(Error error)
    {
        (ErrorCode, ErrorDetail, Error, ErrorTarget, ErrorField) = (error.Code, error.Message, error.Code + ": " + error.Message, StartErrorTarget.Summary, null);
        ErrorChanged();
        ErrorFocusRequests++;
    }

    private void Refuse(string sentence, StartErrorTarget target, StartFieldViewModel? field)
    {
        (ErrorCode, ErrorDetail, Error, ErrorTarget, ErrorField) = (string.Empty, sentence, sentence, target, field);
        ErrorChanged();
        ErrorFocusRequests++;
    }

    private void ClearError()
    {
        if (Error.Length == 0)
        {
            return;
        }

        (ErrorCode, ErrorDetail, Error, ErrorTarget, ErrorField) = (string.Empty, string.Empty, string.Empty, StartErrorTarget.Summary, null);
        ErrorChanged();
    }

    private void ErrorChanged()
    {
        OnChanged(nameof(HasError));
        OnChanged(nameof(ShowsSummary));
        OnChanged(nameof(RecipeError));
        OnChanged(nameof(ItemError));
        Say(Error);
    }

    // What an answer left behind belongs to the item and recipe it answered: another item or recipe starts clean.
    private void ResetAttempt()
    {
        _edits++;
        (_sentFirst, _sentNext) = (false, false);
        Outcome = string.Empty;
        OffersNewAttempt = false;
        StateChanged();
    }
}
