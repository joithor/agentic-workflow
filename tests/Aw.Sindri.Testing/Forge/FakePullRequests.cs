using Aw.Kernel;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Workflow;

namespace Aw.Sindri.Testing.Forge;

/// <summary>An <see cref="IPullRequests"/> in memory: the pull requests a test adds or changes, and how many reads were made.</summary>
public sealed class FakePullRequests : IPullRequests
{
    private readonly Dictionary<string, PullView> _pulls = new(StringComparer.Ordinal);

    /// <summary>How many times <see cref="Get"/> was called.</summary>
    public int Reads { get; private set; }

    /// <summary>Makes the pull request exist.</summary>
    /// <param name="pull">The pull request.</param>
    public void Add(PullView pull)
    {
        ArgumentNullException.ThrowIfNull(pull);
        _pulls[pull.Link] = pull;
    }

    /// <summary>Changes a pull request.</summary>
    /// <param name="link">Its link.</param>
    /// <param name="change">The change.</param>
    public void Change(string link, Func<PullView, PullView> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        _pulls[link] = change(_pulls[link]);
    }

    /// <inheritdoc/>
    public ValueTask<Result<PullView>> Get(string link, CancellationToken cancellationToken)
    {
        Reads++;
        return ValueTask.FromResult<Result<PullView>>(_pulls.TryGetValue(link, out var found) ? found : WorkflowErrors.EffectFailed.With($"no pull request for {link}"));
    }
}
