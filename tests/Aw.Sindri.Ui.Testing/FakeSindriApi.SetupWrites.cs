using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Setup;

namespace Aw.Sindri.Ui.Testing;

/// <summary>
/// The setup writes of the fake (setup-once spec §5, §7.2): each answers a golden (the repo ones are api #528's own, copied to SpecShaped; the Onyx and MCP ones are spec-shaped
/// until their units land), the same text the handler serves. Every call is counted and its body kept, so a test can say how often a press sent and what.
/// </summary>
internal sealed partial class FakeSindriApi
{
    private readonly List<AddRepoRequest> _addedRepos = [];
    private readonly List<RegisterMcpRequest> _registered = [];
    private readonly List<OnyxLogin> _logins = [];
    private readonly List<OnyxDeferRequest> _deferred = [];
    private int _onyxSetups;

    /// <summary>POST /sindri/setup/repos' answer: the started job by default.</summary>
    public RouteAnswer AddRepoAnswer { get; set; } = RouteAnswer.SpecShaped("setup-repos", "started");

    /// <summary>POST /sindri/setup/mcp's answer.</summary>
    public RouteAnswer RegisterMcpAnswer { get; set; } = RouteAnswer.SpecShaped("setup-mcp", "started");

    /// <summary>POST /sindri/onyx/setup's answer.</summary>
    public RouteAnswer SetupOnyxAnswer { get; set; } = RouteAnswer.SpecShaped("onyx-setup", "started");

    /// <summary>POST /sindri/onyx/account's answer.</summary>
    public RouteAnswer ConnectOnyxAnswer { get; set; } = RouteAnswer.SpecShaped("onyx-account", "started");

    /// <summary>POST /sindri/onyx/defer's answer.</summary>
    public RouteAnswer DeferOnyxAnswer { get; set; } = RouteAnswer.SpecShaped("onyx-defer", "deferred");

    /// <summary>When set, an AddRepo waits here (after it was counted) until the gate is completed: a press whose answer is still on its way.</summary>
    public TaskCompletionSource? WriteGate { get; set; }

    /// <summary>Every AddRepo body received, in order.</summary>
    public IReadOnlyList<AddRepoRequest> AddedRepos { get { lock (_gate) { return [.. _addedRepos]; } } }

    /// <summary>Every RegisterMcp body received, in order.</summary>
    public IReadOnlyList<RegisterMcpRequest> RegisteredMcp { get { lock (_gate) { return [.. _registered]; } } }

    /// <summary>How many times Onyx's setup was started.</summary>
    public int SetupOnyxCalls => Volatile.Read(ref _onyxSetups);

    /// <summary>Every ConnectOnyx login received, in order.</summary>
    public IReadOnlyList<OnyxLogin> OnyxLogins { get { lock (_gate) { return [.. _logins]; } } }

    /// <summary>Every DeferOnyx body received, in order.</summary>
    public IReadOnlyList<OnyxDeferRequest> OnyxDeferrals { get { lock (_gate) { return [.. _deferred]; } } }

    public async Task<Result<JobStarted>> AddRepo(Uri api, AddRepoRequest request, CancellationToken cancellationToken)
    {
        if (NotReceived<JobStarted>(api) is { } unanswered)
        {
            return unanswered;
        }

        Keep(_addedRepos, request);
        await WaitFor(WriteGate, cancellationToken).ConfigureAwait(false);
        return Answer<JobStarted>(api, SetupRoutes.Repos, AddRepoAnswer);
    }

    public Task<Result<JobStarted>> RegisterMcp(Uri api, RegisterMcpRequest request, CancellationToken cancellationToken)
    {
        if (NotReceived<JobStarted>(api) is { } unanswered)
        {
            return Task.FromResult(unanswered);
        }

        Keep(_registered, request);
        return Task.FromResult(Answer<JobStarted>(api, SetupRoutes.Mcp, RegisterMcpAnswer));
    }

    public Task<Result<JobStarted>> SetupOnyx(Uri api, CancellationToken cancellationToken)
    {
        if (NotReceived<JobStarted>(api) is { } unanswered)
        {
            return Task.FromResult(unanswered);
        }

        _ = Interlocked.Increment(ref _onyxSetups);
        return Task.FromResult(Answer<JobStarted>(api, SetupRoutes.OnyxSetup, SetupOnyxAnswer));
    }

    public async Task<Result<JobStarted>> ConnectOnyx(Uri api, OnyxLogin login, CancellationToken cancellationToken)
    {
        if (NotReceived<JobStarted>(api) is { } unanswered)
        {
            return unanswered;
        }

        Keep(_logins, login);
        await WaitFor(WriteGate, cancellationToken).ConfigureAwait(false);
        return Answer<JobStarted>(api, SetupRoutes.OnyxAccount, ConnectOnyxAnswer);
    }

    public Task<Result<OnyxDeferView>> DeferOnyx(Uri api, OnyxDeferRequest request, CancellationToken cancellationToken)
    {
        if (NotReceived<OnyxDeferView>(api) is { } unanswered)
        {
            return Task.FromResult(unanswered);
        }

        Keep(_deferred, request);
        return Task.FromResult(Answer<OnyxDeferView>(api, SetupRoutes.OnyxDefer, DeferOnyxAnswer));
    }

    private void Keep<T>(List<T> into, T body)
    {
        lock (_gate)
        {
            into.Add(body);
        }
    }
}
