using Aw.Kernel;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Approvals;

/// <summary>The approve-all dialog (<see cref="ApproveAllViewModel"/>, continued): the plan, its challenge, and the words for a refusal.</summary>
internal sealed partial class ApproveAllViewModel
{
    private const string SessionRefusal = "SND-AUTH-002";
    private const string HoldRefusal = "SND-SETUP-003";
    private const string RunningRefusal = "SND-JOB-409";
    private const string PlanChangedRefusal = "SND-RECIPE-009";

    /// <summary>The workflows the approval covers, in the order it happens (children first).</summary>
    public IReadOnlyList<PlanRowViewModel> Rows { get; private set => Set(ref field, value); } = [];

    /// <summary>The workflows that need their own approval, each with the sentence the server gave.</summary>
    public IReadOnlyList<ApproveAllLine> OwnApproval { get; private set => Set(ref field, value); } = [];

    /// <summary>True when something needs its own approval.</summary>
    public bool HasOwnApproval => OwnApproval.Count > 0;

    /// <summary>"3 workflows will be approved".</summary>
    public string Heading { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The server's sentence when nothing waits; empty otherwise.</summary>
    public string EmptyText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The plan's digest, id and hashes, for Details (the digest and one line each).</summary>
    public IReadOnlyList<string> DetailLines { get; private set => Set(ref field, value); } = [];

    /// <summary>The refusal or failure in words; empty for none.</summary>
    public string Error
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(HasError));
            if (value.Length > 0)
            {
                Live.Say(value);
            }
        }
    } = string.Empty;

    /// <summary>The refusal's code, shown after the words in muted text; empty for none.</summary>
    public string ErrorCode { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when <see cref="Error"/> shows.</summary>
    public bool HasError => Error.Length > 0;

    /// <summary>A line that is not a refusal (the hold was let go; the plan changed; the challenge ran out); empty for none.</summary>
    public string Notice
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(HasNotice));
            if (value.Length > 0)
            {
                Live.Say(value);
            }
        }
    } = string.Empty;

    /// <summary>True when <see cref="Notice"/> shows.</summary>
    public bool HasNotice => Notice.Length > 0;

    /// <summary>The text that redeems the challenge when typed instead of held; empty before a challenge.</summary>
    public string TypedAlternative { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The shortest hold the server accepts, in milliseconds.</summary>
    public int MinHoldMs => Hold.MinHoldMs;

    /// <summary>True when there is a challenge to answer and a plan to approve: the hold and the typed field are live.</summary>
    public bool CanApprove => Phase == ApproveAllPhase.Plan && _challenge is not null && !IsSending && Rows.Count > 0;

    /// <summary>True when the challenge is a held one (the hold button shows); a typed one offers the field alone.</summary>
    public bool OffersHold => _challenge is { } c && string.Equals(c.Confirm, "held", StringComparison.Ordinal);

    // Reads the plan's answer: it fills the lists, then asks for the challenge when there is something to approve.
    private async Task ShowPlan(Uri api, Result<ApproveAllPlan> read, int session, CancellationToken cancellationToken)
    {
        if (read.IsError)
        {
            Fail(read.Error);
            return;
        }

        _plan = read.Value;
        Fill(read.Value);
        if (read.Value.Recipes.Count == 0)
        {
            if (EmptyText.Length == 0)
            {
                EmptyText = HasOwnApproval ? Labels.ApproveOnlyOwn : Labels.ApproveNothingWaiting;   // never a blank, nameless line
            }

            Phase = ApproveAllPhase.Empty;
            return;
        }

        Phase = ApproveAllPhase.Plan;
        await Ask(api, session, cancellationToken).ConfigureAwait(true);
    }

    private void Fill(ApproveAllPlan plan)
    {
        Rows = [.. plan.Recipes.Select(r => new PlanRowViewModel(r))];
        OwnApproval = [.. plan.OwnApproval.Select(OwnLine)];
        EmptyText = plan.Empty ?? string.Empty;
        Heading = Labels.ApprovePlanHeading(plan.Recipes.Count);
        Hold.SetCount(Rows.Count);
        DetailLines = [.. Rows.Select(r => r.Detail).Prepend("plan " + plan.Digest)];
        OnChanged(nameof(HasOwnApproval));
    }

    // The challenge for this plan; the server binds it to the digest, so a plan that changes meanwhile is refused at the answer.
    private async Task Ask(Uri api, int session, CancellationToken cancellationToken)
    {
        if (_plan is not { } plan)
        {
            return;
        }

        if (_asking)
        {
            return;
        }

        var asked = await Request(api, plan, session, cancellationToken).ConfigureAwait(true);
        if (session != _session)
        {
            return;
        }

        if (asked.IsError)
        {
            _challenge = null;
            AskFailed(asked.Error);
            return;
        }

        _challenge = asked.Value;
        TypedAlternative = asked.Value.TypedAlternative ?? asked.Value.Action;
        Hold.Offer(asked.Value);
        TypedBox.Offer(asked.Value);
        Notice = string.Empty;   // "that took too long" is over once the fresh challenge is here
        OnChanged(nameof(CanApprove));
        OnChanged(nameof(CanRetry));
        OnChanged(nameof(OffersHold));
        Live.Say(Labels.ApproveReady);
    }

    // The one challenge request in flight: a second ask waits for it to end.
    private async Task<Result<AppChallengeView>> Request(Uri api, ApproveAllPlan plan, int session, CancellationToken cancellationToken)
    {
        _asking = true;
        OnChanged(nameof(CanRetry));
        try
        {
            return await _api.IssueAppChallenge(api, new AppChallengeRequest(ActionText(plan)), cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            if (session == _session)
            {
                _asking = false;
            }
        }
    }

    // No challenge could be had. A refusal already on show stays (it is why the challenge was asked for); the failure is then a line beside it.
    private void AskFailed(Error error)
    {
        if (HasError)
        {
            Notice = Labels.ApproveAskFailed;
            OnChanged(nameof(CanApprove));
            OnChanged(nameof(CanRetry));
            return;
        }

        Fail(error, keepPlan: true);
    }

    private static ApproveAllLine OwnLine(OwnApprovalRecipe own) =>
        new(WorkflowNames.Of(own.Id, own.DisplayName), own.Sentence, Extra: own.Added.Count > 0 ? Labels.ApproveNewlyAllowed(string.Join("; ", own.Added.Select(a => a.Sentence))) : string.Empty);

    private static string ActionText(ApproveAllPlan plan) => "approve-all " + (plan.Digest.Length > 6 ? plan.Digest[..6] : plan.Digest);

    // A refusal in words: the sentence first, its code after. An agent session is told in plain words that the app is the person's.
    private void Fail(Error error, bool keepPlan = false)
    {
        ErrorCode = error.Code;
        Error = error.Code switch
        {
            SessionRefusal => Labels.ApproveSessionRefused,
            RunningRefusal => Labels.ApproveAlreadyRunning,
            _ => error.Message,
        };
        Phase = keepPlan ? Phase : ApproveAllPhase.Failed;
        OnChanged(nameof(CanApprove));
        OnChanged(nameof(CanRetry));
    }

    private void ResetPlanState()
    {
        (Rows, OwnApproval, DetailLines) = ([], [], []);
        (Heading, EmptyText, Error, ErrorCode, Notice, TypedAlternative) = (string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
        Hold.SetMinHold(DefaultHoldMs);
        Hold.SetCount(0);
        TypedBox.Clear();
        OnChanged(nameof(HasOwnApproval));
        OnChanged(nameof(HasNotice));
    }
}
