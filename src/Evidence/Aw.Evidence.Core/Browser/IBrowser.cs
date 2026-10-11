using Aw.Kernel;

namespace Aw.Evidence.Core.Browser;

/// <summary>Starts headless browser sessions.</summary>
public interface IBrowser
{
    /// <summary>Opens a session (one context and page) with <paramref name="options"/>.</summary>
    /// <param name="options">The session's host, viewport and recording.</param>
    /// <param name="cancellationToken">Cancels the launch.</param>
    /// <returns>The session, or AW-EVIDENCE-013 when the browser can't start.</returns>
    public ValueTask<Result<IBrowserSession>> OpenAsync(BrowserSessionOptions options, CancellationToken cancellationToken);
}
