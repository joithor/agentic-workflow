using Aw.Readiness.App.Features.CheckRepo;

namespace Aw.Readiness.Testing;

/// <summary>Builds a CheckRepoRequest with deterministic defaults.</summary>
internal sealed class CheckRepoRequestBuilder
{
    private string _root = "/repo";
    private bool _useHost = true;

    /// <summary>Sets the repo root.</summary>
    /// <param name="root">The root.</param>
    /// <returns>This builder.</returns>
    public CheckRepoRequestBuilder AtRoot(string root)
    {
        _root = root;
        return this;
    }

    /// <summary>Turns host reads off (--no-remote).</summary>
    /// <returns>This builder.</returns>
    public CheckRepoRequestBuilder Offline()
    {
        _useHost = false;
        return this;
    }

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public CheckRepoRequest Build() => new(_root, _useHost);
}
