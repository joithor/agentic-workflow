using System.Globalization;
using Aw.Evidence.Core;
using Aw.Evidence.Core.Browser;
using Aw.Evidence.Core.Design;
using Aw.Evidence.Core.Geometry;
using Aw.Evidence.Core.Guard;
using Aw.Evidence.Core.Lint;
using Aw.Kernel;

namespace Aw.Evidence.Testing;

/// <summary>An IBrowserSession over a <see cref="FakePage"/>; waits are immediate and every call is recorded.</summary>
/// <param name="page">The page state.</param>
/// <param name="options">The options it was opened with.</param>
public sealed class FakeBrowserSession(FakePage page, BrowserSessionOptions options) : IBrowserSession
{
    private readonly List<string> _calls = [];

    /// <summary>The page state.</summary>
    public FakePage Page { get; } = page;

    /// <summary>The options it was opened with.</summary>
    public BrowserSessionOptions Options { get; } = options;

    /// <summary>Every call, in order ('click testId:save', 'goto /x', …).</summary>
    public IReadOnlyList<string> Calls => _calls;

    /// <summary>Whether the session was disposed.</summary>
    public bool Disposed { get; private set; }

    /// <inheritdoc/>
    public string BrowserVersion => "140.0.0.0";

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> GotoAsync(string path, TimeSpan timeout, CancellationToken cancellationToken)
    {
        _calls.Add($"goto {path}");
        Page.Path = path.Split('?')[0];
        return Ok();
    }

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> ClickAsync(Locator target, TimeSpan timeout, CancellationToken cancellationToken)
    {
        _calls.Add($"click {FakePage.KeyOf(target)}");
        if (!Page.Has(target))
        {
            return Fail($"{FakePage.KeyOf(target)} not found");
        }

        if (Page.OnClick.TryGetValue(FakePage.KeyOf(target), out var effect))
        {
            effect(Page);
        }

        return Ok();
    }

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> FillAsync(Locator target, string value, TimeSpan timeout, CancellationToken cancellationToken)
    {
        _calls.Add($"fill {FakePage.KeyOf(target)}");
        if (!Page.Has(target))
        {
            return Fail($"{FakePage.KeyOf(target)} not found");
        }

        Page.Inputs[InputKey(target)] = value;
        return Ok();
    }

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> WaitVisibleAsync(Locator target, TimeSpan timeout, CancellationToken cancellationToken) =>
        Page.Has(target) ? Ok() : Fail($"{FakePage.KeyOf(target)} not visible");

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> WaitHiddenAsync(Locator target, TimeSpan timeout, CancellationToken cancellationToken) =>
        Page.Has(target) ? Fail($"{FakePage.KeyOf(target)} still visible") : Ok();

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> WaitForPathAsync(string path, TimeSpan timeout, CancellationToken cancellationToken) =>
        Page.Path == path ? Ok() : Fail($"path is {Page.Path}, not {path}");

    /// <inheritdoc/>
    public ValueTask<Result<string>> InputValueAsync(Locator target, TimeSpan timeout, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<string>>(Page.Has(target)
            ? (Page.Inputs.TryGetValue(InputKey(target), out var value) ? value : string.Empty)
            : EvidenceErrors.BrowserFailed.With($"{FakePage.KeyOf(target)} not found"));

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<Candidate>>> VisibleCandidatesAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Candidate> listed = [.. Page.Candidates];
        Page.OnEnumerate?.Invoke(Page);
        return ValueTask.FromResult(Results.Success(listed));
    }

    /// <inheritdoc/>
    public ValueTask<Result<Box>> BoundingBoxAsync(Locator target, TimeSpan timeout, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<Box>>(Page.Boxes.TryGetValue(FakePage.KeyOf(target), out var box)
            ? box
            : EvidenceErrors.BrowserFailed.With($"{FakePage.KeyOf(target)} not found"));

    /// <inheritdoc/>
    public ValueTask<Result<byte[]>> ScreenshotAsync(CancellationToken cancellationToken)
    {
        _calls.Add("screenshot");
        return ValueTask.FromResult<Result<byte[]>>(Page.Screenshot.ToArray());
    }

    /// <inheritdoc/>
    public ValueTask<Result<PageSnapshot>> LintSnapshotAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<PageSnapshot>>(Page.Snapshot);

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> SettleAsync(TimeSpan settle, CancellationToken cancellationToken)
    {
        _calls.Add("settle");
        return Ok();
    }

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> DropFocusAsync(Offset? neutralClick, CancellationToken cancellationToken)
    {
        _calls.Add(neutralClick is null ? "blur" : string.Create(CultureInfo.InvariantCulture, $"blur+click {neutralClick.X},{neutralClick.Y}"));
        return Ok();
    }

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> ResetScrollAsync(CancellationToken cancellationToken)
    {
        _calls.Add("reset-scroll");
        return Ok();
    }

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> SignInAsync(LoginSpec login, Credentials credentials, CancellationToken cancellationToken)
    {
        _calls.Add("sign-in");
        return Page.SignInFails ? Fail(credentials.Redact($"sign-in failed for {credentials.Email}")) : Ok();
    }

    /// <inheritdoc/>
    public ValueTask<Result<SessionRecording>> StopRecordingAsync(string tracePath, CancellationToken cancellationToken)
    {
        _calls.Add($"stop {tracePath}");
        var recording = Options.RecordDirectory is { } dir
            ? new SessionRecording(tracePath, [string.Create(CultureInfo.InvariantCulture, $"{dir.TrimEnd('/')}/video-{Options.Viewport.W}.webm")])
            : new SessionRecording(Trace: null, []);
        return ValueTask.FromResult<Result<SessionRecording>>(recording);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }

    private static string InputKey(Locator target) => target switch
    {
        TestIdLocator t => t.TestId.Value,
        CssLocator c => c.Css,
        _ => FakePage.KeyOf(target),
    };

    private static ValueTask<Result<Unit>> Ok() => ValueTask.FromResult(Results.Ok());

    private static ValueTask<Result<Unit>> Fail(string message) =>
        ValueTask.FromResult<Result<Unit>>(EvidenceErrors.BrowserFailed.With(message));
}
