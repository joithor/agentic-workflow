using System.ComponentModel;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.ViewModels.Components;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Confirmations;

/// <summary>
/// The hold-to-approve button's state (setup spec §2.2). It draws and times the hold; the host owns the challenge and sends the answer. The
/// time is the server's (<see cref="Offer"/>), never a constant of the button: a hold that ends before it sends nothing, and a hold that
/// reaches it is reported once, to the host, which sends. Keyboard auto-repeat and the pointer both come to <see cref="Begin"/>, which
/// ignores a second start; only <see cref="Cancel"/> lets go.
/// </summary>
internal sealed class HoldButtonViewModel : ObservableObject
{
    /// <summary>The shortest hold when no challenge has said otherwise.</summary>
    internal const int DefaultHoldMs = 1500;

    private readonly TimeProvider _clock;
    private readonly Func<bool> _reducedMotion;
    private readonly CatalogViewModel _catalog;
    private long _started;
    private bool _complete;
    private int _count;

    /// <summary>A hold for one of the three approvals the app confirms.</summary>
    /// <param name="action">ApproveProfile, ApproveRecipe or ApproveAllRecipes; any other id is refused.</param>
    /// <param name="catalog">The command catalog: its row says whether the app may confirm or the terminal copy shows.</param>
    /// <param name="clock">The clock the hold is timed on.</param>
    /// <param name="reducedMotion">True when motion is reduced: no animated fill, a stepped count in the words instead.</param>
    internal HoldButtonViewModel(string action, CatalogViewModel catalog, TimeProvider clock, Func<bool>? reducedMotion = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(clock);
        (Action, _clock, _catalog) = (AppConfirm.Require(action), clock, catalog);
        _reducedMotion = reducedMotion ?? (() => false);
        Text = Labels.ApproveHold(MinHoldMs);
        Terminal = MakeTerminal();
        catalog.PropertyChanged += OnCatalogChanged;
    }

    // The button can be made before the catalog is read (the inbox page is built with the shell): the row decides again when it arrives.
    private ProtectedActionViewModel? MakeTerminal() => AppConfirm.Terminal(_catalog, Action, Labels.TerminalVerb(Action), Labels.ConfirmSubject(Action), _clock);

    private void OnCatalogChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(CatalogViewModel.Controls), StringComparison.Ordinal) && (Terminal is null) != AppConfirm.IsReady(_catalog, Action))
        {
            Terminal = MakeTerminal();
            OnChanged(nameof(IsReady));
        }
    }

    /// <summary>The host's say on whether a hold may start now (a challenge is there and has not run out); it may do what it must to get one. Null means always.</summary>
    internal Func<bool>? Gate { get; set; }

    /// <summary>Raised once when a hold starts.</summary>
    internal event EventHandler? Started;

    /// <summary>Raised when a hold is let go before its time.</summary>
    internal event EventHandler? Cancelled;

    /// <summary>The catalog action this button stands for.</summary>
    internal string Action { get; }

    /// <summary>True when the catalog says the app may confirm; otherwise the terminal copy shows in its place.</summary>
    public bool IsReady => Terminal is null;

    /// <summary>The terminal copy for a row that is not ready; null when it is.</summary>
    public ProtectedActionViewModel? Terminal { get; private set => Set(ref field, value); }

    /// <summary>The shortest hold the server accepts, in milliseconds.</summary>
    public int MinHoldMs { get; private set => Set(ref field, value); } = DefaultHoldMs;

    /// <summary>True while the button is held.</summary>
    public bool IsHolding
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(ShowsFill));
        }
    }

    /// <summary>True while the answer is on its way: the button says so and takes no new hold.</summary>
    public bool IsBusy
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(ShowsFill));
        }
    }

    /// <summary>How much of the hold is done, 0 to 1: the fill's width.</summary>
    public double Progress { get; private set => Set(ref field, value); }

    /// <summary>True while the fill shows: held (or answered with a full hold), and motion is not reduced.</summary>
    public bool ShowsFill => (IsHolding || (IsBusy && Progress > 0)) && !_reducedMotion();

    /// <summary>The button's words: the whole hold while idle, the count while held, "Approving..." while the answer is out.</summary>
    public string Text { get; private set => Set(ref field, value); }

    /// <summary>The accessible name: how many it approves and for how long (the count never goes into it).</summary>
    public string Name => Labels.ApproveHoldName(_count, MinHoldMs);

    /// <summary>The fixed instructions for assistive tech.</summary>
    public string Help => Labels.ApproveHold(MinHoldMs);

    /// <summary>Takes the served challenge's shortest hold.</summary>
    /// <param name="challenge">The challenge.</param>
    internal void Offer(AppChallengeView challenge)
    {
        ArgumentNullException.ThrowIfNull(challenge);
        SetMinHold(challenge.MinHoldMs ?? DefaultHoldMs);
    }

    /// <summary>Sets the shortest hold (back to the default when no challenge is held).</summary>
    /// <param name="minHoldMs">Milliseconds.</param>
    internal void SetMinHold(int minHoldMs)
    {
        MinHoldMs = minHoldMs;
        if (!IsHolding && !IsBusy)
        {
            Text = Labels.ApproveHold(minHoldMs);
        }

        OnChanged(nameof(Name));
        OnChanged(nameof(Help));
    }

    /// <summary>Sets how many the approval covers, for the accessible name.</summary>
    /// <param name="count">The count.</param>
    internal void SetCount(int count)
    {
        _count = count;
        OnChanged(nameof(Name));
    }

    /// <summary>Starts a hold (the pointer pressed, or Space or Enter held). A second start while held, or while the answer is out, does nothing.</summary>
    /// <returns>True when a hold started.</returns>
    internal bool Begin()
    {
        if (IsHolding || IsBusy || !IsReady || Gate?.Invoke() == false)
        {
            return false;
        }

        _started = _clock.GetTimestamp();
        _complete = false;
        (IsHolding, Progress) = (true, 0);
        Text = Labels.ApproveHolding(0, MinHoldMs);
        Started?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>One step of the fill (the view calls it on a timer while held).</summary>
    /// <returns>The time held, in milliseconds, once the hold reached the shortest hold (then the hold is over and the host sends); null before.</returns>
    internal int? Tick()
    {
        if (!IsHolding)
        {
            return null;
        }

        var held = (int)_clock.GetElapsedTime(_started).TotalMilliseconds;
        Progress = Math.Min(1d, (double)held / MinHoldMs);
        Text = Labels.ApproveHolding(held, MinHoldMs);
        if (held < MinHoldMs)
        {
            return null;
        }

        (IsHolding, _complete) = (false, true);
        return held;
    }

    /// <summary>The button was let go (or focus left it, or the capture was lost) before the time: nothing is sent.</summary>
    internal void Cancel()
    {
        if (!IsHolding)
        {
            return;
        }

        Reset();
        Cancelled?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The answer is on its way: the button says so and takes no new hold. A complete hold keeps its full fill.</summary>
    internal void Busy()
    {
        IsHolding = false;
        IsBusy = true;
        Progress = _complete ? 1d : 0d;
        Text = Labels.HoldApproving;
    }

    /// <summary>Back to idle, with no event (the answer was refused, or the dialog closed).</summary>
    internal void Reset()
    {
        (IsHolding, IsBusy, _complete, Progress) = (false, false, false, 0d);
        Text = Labels.ApproveHold(MinHoldMs);
    }
}
