using Aw.Kernel;
using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The concepts route of the fake: not served (a 404) until a test serves concepts, so every other Context test sees today's map.</summary>
internal sealed partial class FakeSindriApi
{
    private readonly List<string> _conceptsAsked = [];

    /// <summary>GET /sindri/context/concepts's answer per repo (a 404 by default: a server without the route).</summary>
    public Func<string, RouteAnswer> ContextConceptsAnswer { get; set; } = _ => _notServed;

    /// <summary>The repos the concepts were read for, in order.</summary>
    public IReadOnlyList<string> ContextConceptsAsked { get { lock (_gate) { return [.. _conceptsAsked]; } } }

    /// <summary>Held open until set: a concepts read that started while it was set waits for it.</summary>
    public TaskCompletionSource? ContextConceptsGate { get; set; }

    // Counted apart from ContextCalls, which the Context page's tests hold at zero before a read of the lenses.
    public async Task<Result<ContextConceptsView>> ContextConcepts(Uri api, string repo, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _conceptsAsked.Add(repo);
        }

        await WaitFor(ContextConceptsGate, cancellationToken).ConfigureAwait(false);
        return NotReceived<ContextConceptsView>(api) ?? Answer<ContextConceptsView>(api, "/sindri/context/concepts", ContextConceptsAnswer(repo));
    }
}
