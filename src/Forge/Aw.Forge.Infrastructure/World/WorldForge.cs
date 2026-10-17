using System.Security.Cryptography;
using System.Text;
using Aw.Forge.Core;
using Aw.Forge.Core.Pulls;
using Aw.Forge.Core.Ports;
using Aw.Forge.Core.Refs;
using Aw.Forge.Infrastructure.Git;
using Aw.Kernel;
using Aw.Redaction;

namespace Aw.Forge.Infrastructure.World;

/// <summary>
/// The forge port over a <see cref="IWorldStore"/>: what the fake (memory) and the file adapter (a JSON file) both do. It keeps
/// the real forge's rules that callers depend on: a host outside the allowed set is refused before anything is read, a reply or
/// reaction lands once per marker, a merge checks the expected head, and every change moves the pull request's version stamp.
/// A push goes through git when a <see cref="GitPusher"/> is given, otherwise it is simulated against the world's branch tips.
/// </summary>
internal class WorldForge(ForgeKind kind, ForgeHosts hosts, IWorldStore store, IRedactor redactor, GitPusher? git, string actingLogin = "aw-bot") : IForge, IForgeSeed
{
    private int _requests;

    /// <summary>How many calls got past the host check and touched the store.</summary>
    internal int Requests => Volatile.Read(ref _requests);

    public ForgeKind Kind => kind;

    internal ForgeHosts Hosts => hosts;

    internal IWorldStore Store => store;

    public Result<ForgeRef> Parse(string? link) => ForgeRef.Parse(link, kind, hosts);

    public ValueTask<Result<PullSnapshot>> GetPull(ForgeRef pull, CancellationToken cancellationToken) =>
        Done(Guard(pull.Host) ?? Read(pull, (state, p) => Snapshot(state, p)));

    public ValueTask<Result<IReadOnlyList<PullDigest>>> GetPullDigest(IReadOnlyList<ForgeRef> pulls, CancellationToken cancellationToken)
    {
        var digests = new List<PullDigest>();
        foreach (var pull in pulls)
        {
            var one = Guard(pull.Host) ?? Read(pull, (_, p) => Digest(pull, p));
            if (one.IsError)
            {
                return Done<IReadOnlyList<PullDigest>>(one.Error);
            }

            digests.Add(one.Value);
        }

        return Done<IReadOnlyList<PullDigest>>(digests);
    }

    public ValueTask<Result<IReadOnlyList<ForgeThread>>> ListThreads(ForgeRef pull, CancellationToken cancellationToken) =>
        Done(Guard(pull.Host) ?? Read<IReadOnlyList<ForgeThread>>(pull, (_, p) => [.. p.Threads.Select(ToThread)]));

    public ValueTask<Result<string>> CheckLog(ForgeRef pull, string check, CancellationToken cancellationToken) =>
        Done(Guard(pull.Host) ?? Read(pull, (_, p) => p.Logs.TryGetValue(check, out var log)
            ? Excerpt(log)
            : ForgeErrors.NotFound.With($"check '{check}' has no log")));

    public ValueTask<Result<Delivery>> Reply(ReplyRequest request, CancellationToken cancellationToken)
    {
        var bad = Guard(request.Pull.Host) ?? Cast(ForgeMarker.Validate(request.Marker));
        return Done(bad ?? Write(request.Pull, (_, p) => ReplyTo(p, request)));
    }

    public ValueTask<Result<Delivery>> React(ReactionRequest request, CancellationToken cancellationToken)
    {
        var bad = Guard(request.Pull.Host) ?? Cast(ForgeMarker.Validate(request.Marker));
        return Done(bad ?? Write(request.Pull, (_, p) => ReactTo(p, request)));
    }

    public async ValueTask<Result<PushOutcome>> Push(PushRequest request, CancellationToken cancellationToken)
    {
        if (git is null)
        {
            return Simulate(request);
        }

        var outcome = await git.Push(request, null, cancellationToken).ConfigureAwait(false);
        if (outcome.IsSuccess && outcome.Value == PushOutcome.Pushed)
        {
            RecordTip(request);
        }

        return outcome;
    }

    public ValueTask<Result<ForgeRef?>> FindPull(FindPullQuery query, CancellationToken cancellationToken) =>
        Done(Guard(query.Repo.Host) ?? Count(() => Results.Success<ForgeRef?>(store.Read(state => state.Pulls
            .Where(p => p.Status == PullStatus.Open && Same(p, query.Repo) && p.HeadRef == query.Head
                && (query.Author is null || p.Author == query.Author)
                && (query.Marker is null || ForgeMarker.IsIn(p.Body, query.Marker)))
            .Select(p => new ForgeRef(kind, p.Host, p.Owner, p.Repo, p.Number))
            .FirstOrDefault()))));

    public ValueTask<Result<ForgeRef>> OpenPull(OpenPullRequest request, CancellationToken cancellationToken) =>
        Done(Guard(request.Repo.Host) ?? Count(() => Results.Success(store.Write(state => Open(state, request)))));

    public ValueTask<Result<Unit>> UpdatePull(UpdatePullRequest request, CancellationToken cancellationToken) =>
        Done(Guard(request.Pull.Host) ?? Write(request.Pull, (_, p) =>
        {
            p.Title = request.Title ?? p.Title;
            p.Body = request.Body ?? p.Body;
            return Results.Ok();
        }));

    public ValueTask<Result<Unit>> Retarget(ForgeRef pull, string newBase, CancellationToken cancellationToken) =>
        Done(Guard(pull.Host) ?? Write(pull, (state, p) =>
        {
            p.BaseRef = newBase;
            p.BaseSha = BaseTip(state, p);
            return Results.Ok();
        }));

    public ValueTask<Result<MergeOutcome>> Merge(MergeRequest request, CancellationToken cancellationToken) =>
        Done(Guard(request.Pull.Host) ?? Write(request.Pull, (_, p) => MergePull(p, request)));

    public ValueTask<Result<int>> RerunChecks(ForgeRef pull, string sha, CancellationToken cancellationToken) =>
        Done(Guard(pull.Host) ?? Write(pull, (_, p) =>
        {
            if (!string.Equals(p.ChecksSha, sha, StringComparison.Ordinal))
            {
                return 0;
            }

            var rerun = p.Failing.Count;
            if (rerun > 0)
            {
                p.Checks = ChecksState.Pending;
                p.Failing = [];
                p.Reruns += rerun;
            }

            return rerun;
        }));

    public ValueTask<Result<string>> DefaultBranch(ForgeRepo repo, CancellationToken cancellationToken) =>
        Done(Guard(repo.Host) ?? Count(() => Results.Success(store.Read(state =>
            state.Repos.FirstOrDefault(r => r.Key == repo.Key)?.DefaultBranch ?? "main"))));

    public Result<string> SeedThread(SeedThreadRequest request) =>
        Guard(request.Pull.Host) is { } refused
            ? refused
            : Write(request.Pull, (_, p) =>
            {
                var key = request.Kind.ToString().ToLowerInvariant() + ":" + p.NextComment++.ToString(System.Globalization.CultureInfo.InvariantCulture);
                p.Threads.Add(new WorldThread
                {
                    Key = key,
                    Kind = request.Kind,
                    Author = request.Author,
                    Association = request.Association,
                    IsBot = request.IsBot,
                    Path = request.Path,
                    Body = request.Body,
                });
                return Results.Success(key);
            });

    public Result<Unit> SeedChecks(SeedChecksRequest request) =>
        Guard(request.Pull.Host) is { } refused
            ? refused
            : Write(request.Pull, (_, p) =>
            {
                p.Checks = request.State;
                p.Failing = [.. request.Failing];
                p.Logs = new Dictionary<string, string>(request.Logs, StringComparer.Ordinal);
                return Results.Ok();
            });

    public Result<Unit> AdvanceHead(ForgeRef pull, string sha) =>
        Guard(pull.Host) is { } refused
            ? refused
            : Write(pull, (state, p) =>
            {
                if (p.HeadRemote is not null)
                {
                    state.Tips[Tip(p.HeadRemote, p.HeadRef)] = sha;
                }

                p.HeadSha = sha;
                p.ChecksSha = sha;
                p.Checks = ChecksState.None;
                p.Failing = [];
                return Results.Ok();
            });

    private static ValueTask<Result<T>> Done<T>(Result<T> result) => ValueTask.FromResult(result);

    private static Error? Cast(Result<Unit> result) => result.IsError ? result.Error : null;

    private static bool Same(WorldPull p, ForgeRepo repo) =>
        string.Equals(p.Host, repo.Host, StringComparison.Ordinal)
        && string.Equals(p.Owner, repo.Owner, StringComparison.OrdinalIgnoreCase)
        && string.Equals(p.Repo, repo.Name, StringComparison.OrdinalIgnoreCase);

    private static string Tip(string remote, string branch) => remote + "#" + branch;

    private static string Hash(string text) =>
        Convert.ToHexStringLower(SHA1.HashData(Encoding.UTF8.GetBytes(text)));

    private Result<T> Count<T>(Func<Result<T>> work)
    {
        _ = Interlocked.Increment(ref _requests);
        return work();
    }

    private Error? Guard(string host) =>
        hosts.Allows(host) ? null : ForgeErrors.HostNotAllowed.With($"host '{host}' is not in the forge hosts");

    private Result<TResult> Read<TResult>(ForgeRef pull, Func<WorldState, WorldPull, Result<TResult>> read) =>
        Count(() => store.Read(state => Find(state, pull) is { } p ? read(state, p) : Missing(pull)));

    private Result<TResult> Write<TResult>(ForgeRef pull, Func<WorldState, WorldPull, Result<TResult>> write) =>
        Count(() => store.Write(state =>
        {
            if (Find(state, pull) is not { } p)
            {
                return Missing(pull);
            }

            var result = write(state, p);
            if (result.IsSuccess)
            {
                p.Stamp++;
            }

            return result;
        }));

    private static Error Missing(ForgeRef pull) => ForgeErrors.NotFound.With($"no pull request {pull.Owner}/{pull.Repo}#{pull.Number}");

    private static WorldPull? Find(WorldState state, ForgeRef pull) =>
        state.Pulls.FirstOrDefault(p => p.Number == pull.Number && Same(p, pull.Repository));

    private Result<PullSnapshot> Snapshot(WorldState state, WorldPull p)
    {
        var isDefault = (state.Repos.FirstOrDefault(r => r.Key == Key(p))?.DefaultBranch ?? "main") == p.BaseRef;
        var pull = new ForgeRef(kind, p.Host, p.Owner, p.Repo, p.Number);
        return new PullSnapshot(
            pull,
            pull.Link,
            p.Title,
            p.Author,
            p.Status,
            p.Draft,
            new BaseBranch(p.BaseRef, p.BaseSha, isDefault),
            new HeadBranch(p.HeadRef, p.HeadSha, p.Fork),
            p.Mergeable,
            p.ReviewDecision,
            new ChecksSummary(p.Checks, p.ChecksSha, [.. p.Failing]));
    }

    private static PullDigest Digest(ForgeRef pull, WorldPull p) =>
        new(pull, p.Status, p.HeadSha, p.Checks, p.Threads.Sum(t => 1 + t.Replies.Count), p.Stamp.ToString(System.Globalization.CultureInfo.InvariantCulture));

    private static string Key(WorldPull p) => new ForgeRepo(p.Host, p.Owner, p.Repo).Key;

    private static ForgeThread ToThread(WorldThread t) =>
        new(t.Key, t.Kind, t.Author, t.Association, t.IsBot, t.Resolved, t.Outdated, t.Path, t.Body,
            [.. ForgeMarker.Of(t.Body).Concat(t.Replies.SelectMany(r => ForgeMarker.Of(r)))]);

    private string Excerpt(string log)
    {
        var tail = log.Length > ForgeLimits.LogExcerptCharacters ? log[^ForgeLimits.LogExcerptCharacters..] : log;
        return redactor.Scrub(tail).Text;
    }

    private Result<Delivery> ReplyTo(WorldPull p, ReplyRequest request)
    {
        if (request.ThreadKey == "pull")
        {
            if (p.Threads.Any(t => t.Kind == CommentKind.Comment && ForgeMarker.IsIn(t.Body, request.Marker)))
            {
                return Delivery.AlreadyDelivered;
            }

            p.Threads.Add(new WorldThread
            {
                Key = "comment:" + p.NextComment++.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Kind = CommentKind.Comment,
                Author = actingLogin,
                Association = "OWNER",
                Body = ForgeMarker.Append(request.Text, request.Marker),
            });
            return Delivery.Delivered;
        }

        if (p.Threads.FirstOrDefault(t => t.Key == request.ThreadKey) is not { } thread)
        {
            return ForgeErrors.NotFound.With($"no thread '{request.ThreadKey}'");
        }

        if (ForgeMarker.IsIn(thread.Body, request.Marker) || thread.Replies.Any(r => ForgeMarker.IsIn(r, request.Marker)))
        {
            return Delivery.AlreadyDelivered;
        }

        thread.Replies.Add(ForgeMarker.Append(request.Text, request.Marker));
        return Delivery.Delivered;
    }

    private static readonly HashSet<string> Reactions = new(["+1", "-1", "laugh", "confused", "heart", "hooray", "rocket", "eyes"], StringComparer.Ordinal);

    private static Result<Delivery> ReactTo(WorldPull p, ReactionRequest request)
    {
        if (!Reactions.Contains(request.Reaction))
        {
            return ForgeErrors.Invalid.With("reaction: +1, -1, laugh, confused, heart, hooray, rocket or eyes");
        }

        if (p.Threads.FirstOrDefault(t => t.Key == request.ThreadKey) is not { } thread)
        {
            return ForgeErrors.NotFound.With($"no thread '{request.ThreadKey}'");
        }

        var entry = request.Reaction + ":" + request.Marker;
        if (thread.Reactions.Contains(entry))
        {
            return Delivery.AlreadyDelivered;
        }

        thread.Reactions.Add(entry);
        return Delivery.Delivered;
    }

    private ForgeRef Open(WorldState state, OpenPullRequest request)
    {
        var key = request.Repo.Key;
        var repo = state.Repos.FirstOrDefault(r => r.Key == key);
        if (repo is null)
        {
            repo = new WorldRepo { Key = key };
            state.Repos.Add(repo);
        }

        var remote = state.Tips.FirstOrDefault(t => t.Key.EndsWith("#" + request.Head, StringComparison.Ordinal) && t.Value == request.HeadSha).Key;
        var number = repo.NextNumber++;
        var pull = new WorldPull
        {
            Host = request.Repo.Host,
            Owner = request.Repo.Owner,
            Repo = request.Repo.Name,
            Number = number,
            Title = request.Title,
            Body = request.Body,
            Author = actingLogin,
            Draft = request.Draft,
            BaseRef = request.Base,
            HeadRef = request.Head,
            HeadSha = request.HeadSha,
            HeadRemote = remote?[..remote.LastIndexOf('#')],
            ChecksSha = request.HeadSha,
        };
        pull.BaseSha = BaseTip(state, pull);
        state.Pulls.Add(pull);
        return new ForgeRef(kind, pull.Host, pull.Owner, pull.Repo, number);
    }

    private static string BaseTip(WorldState state, WorldPull p)
    {
        var tip = state.Tips.FirstOrDefault(t => t.Key.EndsWith("#" + p.BaseRef, StringComparison.Ordinal)).Value;
        return tip ?? Hash("base:" + p.BaseRef);
    }

    private Result<MergeOutcome> MergePull(WorldPull p, MergeRequest request)
    {
        if (p.Status != PullStatus.Open)
        {
            return new MergeOutcome(MergeStatus.Blocked, null, p.Status == PullStatus.Merged ? "already merged" : "closed");
        }

        if (!string.Equals(p.HeadSha, request.ExpectedHead, StringComparison.Ordinal))
        {
            return new MergeOutcome(MergeStatus.HeadMoved, null, null);
        }

        var blocked = p switch
        {
            { Draft: true } => "draft",
            { Checks: ChecksState.Failing or ChecksState.Pending } => "checks",
            { ReviewDecision: "CHANGES_REQUESTED" } => "changes requested",
            { Mergeable: false } => "conflicts",
            _ => null,
        };
        if (blocked is not null)
        {
            return new MergeOutcome(MergeStatus.Blocked, null, blocked);
        }

        p.Status = PullStatus.Merged;
        p.MergedSha = Hash($"merge:{request.Method}:{p.HeadSha}");
        return new MergeOutcome(MergeStatus.Merged, p.MergedSha, null);
    }

    private Result<PushOutcome> Simulate(PushRequest request) =>
        Count(() => store.Write(state =>
        {
            var key = Tip(request.Remote, request.Branch);
            var has = state.Tips.TryGetValue(key, out var tip);
            if (request.LeaseSha is null ? has : tip != request.LeaseSha)
            {
                return Results.Success(PushOutcome.HeadMoved);
            }

            Apply(state, request);
            return Results.Success(PushOutcome.Pushed);
        }));

    private void RecordTip(PushRequest request) => store.Write(state =>
    {
        Apply(state, request);
        return Results.Ok();
    });

    private static void Apply(WorldState state, PushRequest request)
    {
        state.Tips[Tip(request.Remote, request.Branch)] = request.Sha;
        foreach (var p in state.Pulls.Where(p => p.HeadRef == request.Branch && p.HeadRemote == request.Remote))
        {
            p.HeadSha = request.Sha;
            p.Checks = ChecksState.None;
            p.ChecksSha = request.Sha;
            p.Failing = [];
            p.Stamp++;
        }
    }
}
