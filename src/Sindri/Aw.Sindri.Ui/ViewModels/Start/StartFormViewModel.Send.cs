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

        var form = _form;
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

        if (form != _form)
        {
            return;   // the form was closed and opened again: this answer is for the one that sent it
        }

        if (answer.IsError)
        {
            Fail(answer.Error);
            return;
        }

        Accepted(answer.Value, next);
    }

    // What goes on the wire, or null with the reason shown: the recipe and the item first, then each input in the recipe's order.
    private StartInstanceRequest? Build(bool next)
    {
        if (Recipe is not { } recipe)
        {
            Refuse(Labels.StartChooseRecipe);
            return null;
        }

        var item = ItemId.Trim();
        if (item.Length == 0)
        {
            Refuse(Labels.StartChooseItem);
            return null;
        }

        var checks = Fields.Select(f => (f.Key, Check: f.Check())).ToList();
        if (checks.FirstOrDefault(c => c.Check.Problem.Length > 0) is { Check.Problem.Length: > 0 } wrong)
        {
            Refuse(wrong.Check.Problem);
            return null;
        }

        var facts = new Dictionary<string, StartFact>(StringComparer.Ordinal);
        foreach (var (key, check) in checks.Where(c => c.Check.Fact is not null))
        {
            facts[key] = check.Fact!;   // why: filtered to the facts that exist
        }

        if (_itemKey is { } itemKey)
        {
            // SND-WF-014: a recipe's item input must name the request's item, so it is made from the one picked
            facts[itemKey] = new StartFact(ItemFactName, Encoding.UTF8.GetBytes(new JsonObject { ["id"] = item }.ToJsonString()));
        }

        return new StartInstanceRequest(recipe.Id, item, next, facts);
    }

    private void Accepted(StartedView started, bool next)
    {
        if (next)
        {
            _sentNext = true;
        }
        else
        {
            _sentFirst = true;
        }

        var instance = started.Instance;
        OffersNewAttempt = false;
        Outcome = !started.Existing ? string.Empty : started.Finished ? Labels.StartFinished(instance.Address) : Labels.StartAlreadyRunning(instance.Address);
        OffersNewAttempt = started.Existing && started.Finished && !_sentNext;
        OnChanged(nameof(CanStart));
        Started?.Invoke(this, new StartedEventArgs(instance.Id, instance.Address, started.Existing, started.Finished));
        if (!started.Existing)
        {
            Close();   // a new instance: its frame is open, there is nothing more to say here
        }
    }

    private void Fail(Error error)
    {
        (ErrorCode, ErrorDetail, Error) = (error.Code, error.Message, error.Code + ": " + error.Message);
        OnChanged(nameof(HasError));
        ErrorFocusRequests++;
    }

    private void Refuse(string sentence)
    {
        (ErrorCode, ErrorDetail, Error) = (string.Empty, sentence, sentence);
        OnChanged(nameof(HasError));
        ErrorFocusRequests++;
    }

    private void ClearError()
    {
        if (Error.Length == 0)
        {
            return;
        }

        (ErrorCode, ErrorDetail, Error) = (string.Empty, string.Empty, string.Empty);
        OnChanged(nameof(HasError));
    }
}
