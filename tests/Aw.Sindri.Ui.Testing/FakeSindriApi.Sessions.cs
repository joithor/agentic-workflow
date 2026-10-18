using Aw.Kernel;
using Aw.Sindri.Ui.Api.Sessions;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The session reads of the fake (collector spec CA-1, CA-2): each answers the API's goldens (<see cref="SessionAnswers"/>), the same text the handler serves.</summary>
internal sealed partial class FakeSindriApi
{
    /// <summary>When set, both session reads answer this instead of the golden (a 503 problem, an odd body).</summary>
    public RouteAnswer? SessionAnswer { get; set; }

    /// <summary>What GET /sessions answers.</summary>
    /// <returns>The answer.</returns>
    internal RouteAnswer SessionListAnswer() => SessionAnswer ?? SessionAnswers.List();

    /// <summary>What GET /sessions/{provider}/{id} answers.</summary>
    /// <param name="provider">The provider.</param>
    /// <param name="id">The session id.</param>
    /// <returns>The answer.</returns>
    internal RouteAnswer SessionOneAnswer(string provider, string id) => SessionAnswer ?? SessionAnswers.One(provider, id);

    public Task<Result<SessionList>> Sessions(Uri api, SessionQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<Result<SessionView>> Session(Uri api, string provider, string id, CancellationToken cancellationToken) => throw new NotSupportedException();
}
