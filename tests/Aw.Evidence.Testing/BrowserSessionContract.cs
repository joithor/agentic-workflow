using Aw.Evidence.Core;
using Aw.Evidence.Core.Browser;
using Aw.Evidence.Core.Ids;
using Aw.Kernel;
using Xunit;

namespace Aw.Evidence.Testing;

/// <summary>
/// The IBrowserSession contract: the semantics the step runner and parity rely on, which a fake kinder than Chromium
/// would hide. Each test opens a session on the contract page at path '/', which holds: a visible text 'Hospitals'; a
/// visible button data-testid 'open' (text 'Open') whose click shows the text 'Panel open' (hidden before); a visible
/// input data-testid 'search'; a hidden button data-testid 'hidden-button'; and a visible link data-testid 'next' to
/// '/next'. The session records nothing (no record directory).
/// </summary>
public abstract class BrowserSessionContract
{
    /// <summary>How long a wait that should fail is given (the adapter really waits it out).</summary>
    protected static readonly TimeSpan FailingWait = TimeSpan.FromMilliseconds(800);

    /// <summary>How long a wait that should succeed is given.</summary>
    protected static readonly TimeSpan PassingWait = TimeSpan.FromSeconds(10);

    private static readonly Locator _open = Locator.ByTestId(TestId.From("open"));
    private static readonly Locator _missing = Locator.ByTestId(TestId.From("no-such-element"));

    /// <summary>Opens a session on the contract page (above).</summary>
    /// <returns>The session; the test disposes it.</returns>
    protected abstract Task<IBrowserSession> OpenContractPageAsync();

    /// <summary>A visible element is found; a missing one is AW-EVIDENCE-013 once the wait runs out.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_visible_element_is_found_and_a_missing_one_is_a_browser_failure()
    {
        await using var session = await OpenContractPageAsync();

        Expect.That((await session.WaitVisibleAsync(_open, PassingWait, CancellationToken.None)).IsSuccess, "a visible test id is found");
        Expect.That((await session.WaitVisibleAsync(Locator.ByText("Hospitals"), PassingWait, CancellationToken.None)).IsSuccess, "a visible text is found");
        IsBrowserFailure(await session.WaitVisibleAsync(_missing, FailingWait, CancellationToken.None), "a missing element");
    }

    /// <summary>A hidden or absent text waits as hidden; a visible one doesn't.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_hidden_text_is_hidden_and_a_visible_one_is_not()
    {
        await using var session = await OpenContractPageAsync();

        Expect.That((await session.WaitHiddenAsync(Locator.ByText("Panel open"), PassingWait, CancellationToken.None)).IsSuccess, "a hidden text is hidden");
        IsBrowserFailure(await session.WaitHiddenAsync(Locator.ByText("Hospitals"), FailingWait, CancellationToken.None), "a visible text waited on as hidden");
    }

    /// <summary>A click runs the element's effect.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_click_runs_the_elements_effect()
    {
        await using var session = await OpenContractPageAsync();

        Expect.That((await session.ClickAsync(_open, PassingWait, CancellationToken.None)).IsSuccess, "the click succeeds");
        Expect.That((await session.WaitVisibleAsync(Locator.ByText("Panel open"), PassingWait, CancellationToken.None)).IsSuccess, "its effect shows");
    }

    /// <summary>A click on a missing element and a fill on a hidden one are browser failures.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Acting_on_a_missing_or_hidden_element_is_a_browser_failure()
    {
        await using var session = await OpenContractPageAsync();

        IsBrowserFailure(await session.ClickAsync(_missing, FailingWait, CancellationToken.None), "a click on a missing element");
        IsBrowserFailure(await session.FillAsync(Locator.ByTestId(TestId.From("hidden-button")), "x", FailingWait, CancellationToken.None), "a fill on a hidden element");
    }

    /// <summary>A filled input reads back its value.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_filled_input_reads_back_its_value()
    {
        await using var session = await OpenContractPageAsync();
        var search = Locator.ByTestId(TestId.From("search"));

        Expect.That((await session.FillAsync(search, "St", PassingWait, CancellationToken.None)).IsSuccess, "the fill succeeds");
        Expect.That((await session.InputValueAsync(search, PassingWait, CancellationToken.None)).Value == "St", "the value reads back");
    }

    /// <summary>The path wait holds for the current path, fails for another, and follows a navigation.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task The_path_wait_follows_the_page()
    {
        await using var session = await OpenContractPageAsync();

        Expect.That((await session.WaitForPathAsync("/", PassingWait, CancellationToken.None)).IsSuccess, "the current path holds");
        IsBrowserFailure(await session.WaitForPathAsync("/elsewhere", FailingWait, CancellationToken.None), "another path");
        Expect.That((await session.ClickAsync(Locator.ByTestId(TestId.From("next")), PassingWait, CancellationToken.None)).IsSuccess, "the link is clicked");
        Expect.That((await session.WaitForPathAsync("/next", PassingWait, CancellationToken.None)).IsSuccess, "the path follows the navigation");
    }

    /// <summary>A goto target that could leave the host is refused.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_goto_target_that_could_leave_the_host_is_refused()
    {
        await using var session = await OpenContractPageAsync();

        IsBrowserFailure(await session.GotoAsync("//evil.test/", PassingWait, CancellationToken.None), "a scheme-relative goto");
        Expect.That((await session.WaitForPathAsync("/", PassingWait, CancellationToken.None)).IsSuccess, "the page stays where it was");
    }

    /// <summary>A visible element has a box; a missing one is a browser failure.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_visible_element_has_a_bounding_box_and_a_missing_one_has_none()
    {
        await using var session = await OpenContractPageAsync();

        var box = await session.BoundingBoxAsync(_open, PassingWait, CancellationToken.None);

        Expect.That(box.IsSuccess && box.Value.W > 0 && box.Value.H > 0, "a visible element has a box with a size");
        IsBrowserFailure(await session.BoundingBoxAsync(_missing, FailingWait, CancellationToken.None), "a missing element's box");
    }

    /// <summary>Visible candidates list the visible controls with their role, never a hidden one, and a candidate can be clicked.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Visible_candidates_list_visible_controls_and_can_be_clicked()
    {
        await using var session = await OpenContractPageAsync();

        var candidates = (await session.VisibleCandidatesAsync(CancellationToken.None)).Value;
        var open = candidates.Single(static c => c.TestId?.Value == "open");

        Expect.That(open.Role == "button", "a button's role is 'button'");
        Expect.That(candidates.All(static c => c.TestId?.Value != "hidden-button"), "a hidden control is never offered");
        Expect.That((await session.ClickAsync(new CandidateLocator(open.Index, open.TestId), PassingWait, CancellationToken.None)).IsSuccess, "a candidate is clicked by index");
        Expect.That((await session.WaitVisibleAsync(Locator.ByText("Panel open"), PassingWait, CancellationToken.None)).IsSuccess, "its effect shows");
    }

    /// <summary>A session without a record directory stops with no trace and no video.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_session_that_records_nothing_stops_with_no_trace_or_video()
    {
        await using var session = await OpenContractPageAsync();

        var recording = await session.StopRecordingAsync("/nowhere/trace.zip", CancellationToken.None);

        Expect.That(recording.IsSuccess && recording.Value.Trace is null && recording.Value.Videos.Count == 0, "nothing was recorded");
    }

    /// <summary>A cancelled token throws instead of acting.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_cancelled_call_throws()
    {
        await using var session = await OpenContractPageAsync();
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();

        var threw = false;
        try
        {
            _ = await session.WaitVisibleAsync(_open, PassingWait, cancel.Token);
        }
        catch (OperationCanceledException)
        {
            threw = true;
        }

        Expect.That(threw, "a cancelled call throws OperationCanceledException");
    }

    private static void IsBrowserFailure<T>(Result<T> result, string what) =>
        Expect.That(result.IsError && result.Error.Code == EvidenceErrors.BrowserFailed.Code, $"{what} is AW-EVIDENCE-013");
}
