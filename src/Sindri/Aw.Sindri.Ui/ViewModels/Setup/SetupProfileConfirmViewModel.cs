using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.ViewModels.Confirmations;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Setup;

/// <summary>
/// The profile row's typed confirmation (setup spec §2.1, §8.3): while the catalog lets the app approve settings, the person types the phrase the server issued for the live
/// profile's hash and the app sends that answer, once. A phrase that ran out, or a profile that changed meanwhile (SND-PROFILE-003), is said in words and a fresh phrase is
/// asked for. While the catalog does not let the app, the row keeps the terminal copy of its action; nothing here sends a protected action's id.
/// </summary>
internal sealed class SetupProfileConfirmViewModel : ObservableObject
{
    private const string Action = "ApproveProfile";
    private const string SessionRefusal = "SND-AUTH-002";
    private const string ChangedRefusal = "SND-PROFILE-003";
    private const int ShortHash = 6;

    private readonly CatalogViewModel _catalog;
    private readonly Func<Uri?> _url;
    private readonly Func<string?> _hash;
    private readonly ISindriApi _api;
    private readonly TimeProvider _clock;
    private AppChallengeView? _challenge;
    private string _asked = string.Empty;   // the hash the challenge in hand was asked for
    private string _summaryFor = string.Empty;   // the hash the summary in hand was read for
    private bool _asking;
    private bool _sending;
    private bool _wanted;
    private int _session;

    internal SetupProfileConfirmViewModel(CatalogViewModel catalog, Func<Uri?> url, Func<string?> hash, ISindriApi api, TimeProvider clock)
    {
        (_catalog, _url, _hash, _api, _clock) = (catalog, url, hash, api, clock);
        TypedBox = new TypedConfirmBoxViewModel(Action, catalog, clock);
    }

    /// <summary>The plain-words summary of what approving lets Sindri do (shown while the row needs the person, whether or not the app may approve).</summary>
    public ProfileSummaryViewModel Summary { get; } = new();

    /// <summary>The typed box (the shared control's state).</summary>
    public TypedConfirmBoxViewModel TypedBox { get; }

    /// <summary>The refusal of the last answer in words; empty for none.</summary>
    public string Refusal
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(HasRefusal));
        }
    } = string.Empty;

    /// <summary>True when there is a refusal to show.</summary>
    public bool HasRefusal => Refusal.Length > 0;

    /// <summary>True once the approval went through.</summary>
    public bool IsDone { get; private set => Set(ref field, value); }

    /// <summary>True when the catalog lets the app approve settings.</summary>
    internal bool IsReady => AppConfirm.IsReady(_catalog, Action);

    /// <summary>True when the typed box shows: the app may approve, the row asks for it, there is a profile hash and it was not approved yet.</summary>
    public bool ShowsTyped => _wanted && IsReady && Hash.Length > 0 && !IsDone;

    /// <summary>True when the app may approve but the profile's hash is not known yet.</summary>
    public bool IsWaiting => _wanted && IsReady && Hash.Length == 0 && !IsDone;

#pragma warning disable CA1822, MA0041 // why: the view binds it as an instance property of the view model
    /// <summary>The line shown while waiting for the hash.</summary>
    public string WaitingText => Labels.SetupProfileReading;
#pragma warning restore CA1822, MA0041

    private string Hash => _hash() ?? string.Empty;

    /// <summary>Follows the row: asked for while the row needs the person, dropped when it does not. Asks for a phrase once, and again only when the profile's hash changed.</summary>
    /// <param name="wanted">True while the profile step needs the person.</param>
    /// <returns>A task that completes when the phrase was asked for (or nothing needed asking).</returns>
    internal Task Follow(bool wanted)
    {
        _wanted = wanted;
        Announce();
        if (!wanted)
        {
            Reset();
            return Task.CompletedTask;
        }

        var read = ReadSummary(_session);
        if (!ShowsTyped || _sending || _asking || (_challenge is not null && string.Equals(_asked, Hash, StringComparison.Ordinal)))
        {
            return read;
        }

        return Task.WhenAll(read, Ask(_session));
    }

    /// <summary>Sends the typed answer, once. A phrase that ran out is asked for again first and nothing is sent.</summary>
    /// <param name="typed">The typed text, exactly the phrase.</param>
    /// <returns>A task that completes when the answer came.</returns>
    internal async Task SubmitTyped(string typed)
    {
        ArgumentNullException.ThrowIfNull(typed);
        if (_sending || IsDone || _challenge is not { } challenge || _url() is not { } api || !string.Equals(typed.Trim(), TypedBox.Phrase, StringComparison.Ordinal))
        {
            return;   // only the served text, exactly, is an answer: the box checks it and so does this
        }

        var session = _session;
        if (challenge.ExpiresAt <= _clock.GetUtcNow())
        {
            Refusal = Labels.NeedsYouTypedExpired;
            await Ask(session).ConfigureAwait(true);
            return;
        }

        (_sending, Refusal) = (true, string.Empty);
        TypedBox.SetSending(sending: true);
        Result<ProfileApproved> sent;
        try
        {
            sent = await _api.ApproveProfile(api, new ApproveProfileRequest(_asked, new ProfileAnswer(challenge.Challenge, typed)), CancellationToken.None).ConfigureAwait(true);
        }
        finally
        {
            if (session == _session)
            {
                (_sending, _challenge) = (false, null);   // any answer uses the challenge up
                TypedBox.SetSending(sending: false);
            }
        }

        if (session == _session)
        {
            await Settle(sent, session).ConfigureAwait(true);
        }
    }

    // An approval is the row's end; anything else is a refusal in words and, but for an agent session, a fresh phrase for the profile as it is now.
    private async Task Settle(Result<ProfileApproved> sent, int session)
    {
        if (sent.IsSuccess && sent.Value.Approved)
        {
            IsDone = true;
            Announce();
            return;
        }

        var refused = sent.IsError ? sent.Error : null;
        Refusal = refused is null ? Labels.NeedsYouNotApproved : Words(refused);
        if (refused is null || !string.Equals(refused.Code, SessionRefusal, StringComparison.Ordinal))
        {
            await Ask(session).ConfigureAwait(true);
        }
    }

    // The summary for the live profile, read once per hash; a failed read is tried again at the next follow, and the person can still approve without it.
    private async Task ReadSummary(int session)
    {
        if (_url() is not { } api || Hash is not { Length: > 0 } hash || string.Equals(hash, _summaryFor, StringComparison.Ordinal))
        {
            return;
        }

        _summaryFor = hash;
        var read = await _api.ProfileSummary(api, CancellationToken.None).ConfigureAwait(true);
        if (session != _session)
        {
            return;
        }

        if (read.IsError)
        {
            _summaryFor = string.Empty;
            return;
        }

        Summary.Show(read.Value.Summary);
    }

    // The phrase for 'approve profile <hash6>'; its served text is what the box shows and the person types.
    private async Task Ask(int session)
    {
        if (_url() is not { } api || Hash is not { Length: > 0 } hash)
        {
            return;
        }

        _asking = true;
        Result<AppChallengeView> asked;
        try
        {
            asked = await _api.IssueAppChallenge(api, new AppChallengeRequest("approve profile " + (hash.Length > ShortHash ? hash[..ShortHash] : hash)), CancellationToken.None).ConfigureAwait(true);
        }
        finally
        {
            if (session == _session)
            {
                _asking = false;
            }
        }

        if (session != _session)
        {
            return;
        }

        if (asked.IsError)
        {
            (_challenge, Refusal) = (null, Words(asked.Error));
        }
        else
        {
            (_challenge, _asked) = (asked.Value, hash);
            TypedBox.Clear();
            TypedBox.Offer(asked.Value);
        }
    }

    // A refusal in words: a changed profile asks to read it again, an agent session is told the app is the person's.
    private static string Words(Error error) => error.Code switch
    {
        ChangedRefusal => Labels.SetupProfileChangedWhileReading,
        SessionRefusal => Labels.ApproveSessionRefused,
        _ => error.Message,
    };

    private void Reset()
    {
        _session++;
        (_challenge, _asked, _summaryFor, _asking, _sending) = (null, string.Empty, string.Empty, false, false);
        Summary.Show(summary: null);
        TypedBox.Clear();
    }

    private void Announce()
    {
        OnChanged(nameof(ShowsTyped));
        OnChanged(nameof(IsWaiting));
        OnChanged(nameof(HasRefusal));
    }
}
