using Aw.Evidence.Core;
using Aw.Evidence.Core.Browser;
using Aw.Kernel;

namespace Aw.Evidence.Testing;

/// <summary>An IBrowser that opens <see cref="FakeBrowserSession"/>s, one page state per viewport width.</summary>
public sealed class FakeBrowser : IBrowser
{
    private readonly List<FakeBrowserSession> _sessions = [];

    /// <summary>Builds the page for a new session; by default an empty page.</summary>
    public Func<BrowserSessionOptions, FakePage> PageFor { get; set; } = static _ => new FakePage();

    /// <summary>When true, the browser fails to start.</summary>
    public bool FailsToStart { get; set; }

    /// <summary>Every session opened.</summary>
    public IReadOnlyList<FakeBrowserSession> Sessions => _sessions;

    /// <inheritdoc/>
    public ValueTask<Result<IBrowserSession>> OpenAsync(BrowserSessionOptions options, CancellationToken cancellationToken)
    {
        if (FailsToStart)
        {
            return ValueTask.FromResult<Result<IBrowserSession>>(EvidenceErrors.BrowserFailed.With("browser failed to start"));
        }

        var session = new FakeBrowserSession(PageFor(options), options);
        _sessions.Add(session);
        return ValueTask.FromResult<Result<IBrowserSession>>(session);
    }
}
