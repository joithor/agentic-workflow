using Aw.Kernel;
using Aw.Sindri.Ui.Api.CodeIndex;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The code index's read of the fake (CI-10b): GET /sindri/code-index/status answers an API golden, the same text the handler serves.</summary>
internal sealed partial class FakeSindriApi
{
    private int _codeIndexCalls;

    /// <summary>GET /sindri/code-index/status's answer: the one-repo golden by default.</summary>
    public RouteAnswer CodeIndexStatusAnswer { get; set; } = RouteAnswer.Api("code-index-status", "one-repo");

    /// <summary>How many times the status was read.</summary>
    public int CodeIndexCalls => Volatile.Read(ref _codeIndexCalls);

    public Task<Result<CodeIndexStatusView>> CodeIndexStatus(Uri api, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _codeIndexCalls);
        return Task.FromResult(Answer<CodeIndexStatusView>(api, "/sindri/code-index/status", CodeIndexStatusAnswer));
    }
}
