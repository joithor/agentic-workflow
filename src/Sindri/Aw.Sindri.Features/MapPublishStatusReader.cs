using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Features;

/// <summary>
/// Reads the stored publish state for the caller's repos, the index's commits and the ledger's last 'published' and 'promoted' events, and puts
/// them through <see cref="MapPublishStatusRules"/>. The one reader behind 'context map status', the Health card and doctor's 'context:publish'.
/// A session's repos are cut by the engine (its PublishStatus call), so nothing here sees another repo.
/// </summary>
/// <param name="engine">The context engine.</param>
/// <param name="outlooks">The code index's side of the publish gate.</param>
/// <param name="ledger">The ledger, for how the current map was published.</param>
/// <param name="clock">The time.</param>
internal sealed class MapPublishStatusReader(IContextEngine engine, IIndexOutlooks outlooks, ILedgerStore ledger, TimeProvider clock)
{
    /// <summary>The status as the caller of <paramref name="scope"/> may see it.</summary>
    /// <param name="scope">The profile's context section and repos, and the caller.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The status, SND-CONTEXT-030 when there is neither a map nor a hold, or the engine's refusal.</returns>
    internal async ValueTask<Result<MapPublishStatusView>> Read(ContextScope scope, CancellationToken cancellationToken)
    {
        var view = await engine.PublishStatus(scope, cancellationToken).ConfigureAwait(false);
        if (view.IsError)
        {
            return view.Error;
        }

        if (view.Value.Current is null && view.Value.Hold is null)
        {
            return ContextErrors.NoMap.With("there is no map yet, so nothing has been published (fix: sindri context map import <file>, or sindri context map build)");
        }

        var settings = SettingsReader.Read(scope.Settings);
        if (settings.IsError)
        {
            return settings.Error;
        }

        var facts = view.Value.Hold is { } hold
            ? await Held(view.Value, hold, settings.Value, cancellationToken).ConfigureAwait(false)
            : await Current(view.Value, cancellationToken).ConfigureAwait(false);
        return facts.IsError ? facts.Error : MapPublishStatusRules.Of(facts.Value, clock.GetUtcNow());
    }

    private async ValueTask<Result<MapPublishFacts>> Held(MapPublishView view, MapHold hold, ContextSettings settings, CancellationToken cancellationToken)
    {
        var state = new MapHoldState(hold.Version, hold.Since, hold.Heads.ToDictionary(static h => h.Repo.Value, static h => new MapRepoHead(h.Commit, h.OnDefault), StringComparer.Ordinal));
        var read = await outlooks.Read(state, cancellationToken).ConfigureAwait(false);
        var commits = await outlooks.IndexCommits([.. hold.Heads.Select(static h => h.Repo.Value)], cancellationToken).ConfigureAwait(false);
        if (read.IsError || commits.IsError)
        {
            return read.IsError ? read.Error : commits.Error;
        }

        var repos = hold.Heads.Select(h =>
        {
            var outlook = read.Value.TryGetValue(h.Repo.Value, out var detail) ? detail : null;
            var waiting = outlook is { Outlook: IndexOutlook.Coming or IndexOutlook.Backoff };
            return new PublishRepoFact(h.Repo.Value, h.Commit, commits.Value.GetValueOrDefault(h.Repo.Value), waiting, outlook?.Word ?? RepoWait.None);
        });
        return new MapPublishFacts(hold.Version, hold.Since, TimeSpan.FromMinutes(settings.Publish.WaitForIndexMinutes), view.Current, [.. repos], Reason: null, ByHand: false);
    }

    private async ValueTask<Result<MapPublishFacts>> Current(MapPublishView view, CancellationToken cancellationToken)
    {
        var commits = await outlooks.IndexCommits([.. view.CurrentHeads.Select(static h => h.Repo.Value)], cancellationToken).ConfigureAwait(false);
        var published = await ledger.Latest(EventTypes.ContextMapPublished, cancellationToken).ConfigureAwait(false);
        var promoted = await ledger.Latest(EventTypes.ContextMapPromoted, cancellationToken).ConfigureAwait(false);
        if (commits.IsError || published.IsError || promoted.IsError)
        {
            return commits.IsError ? commits.Error : published.IsError ? published.Error : promoted.Error;
        }

        var repos = view.CurrentHeads.Select(h => new PublishRepoFact(h.Repo.Value, h.Commit, commits.Value.GetValueOrDefault(h.Repo.Value), Waiting: false, RepoWait.None));
        var reason = published.Value is { } last && About(last, view.Current) is { } p ? Text(p, "reason") : null;
        var byHand = promoted.Value is { } latest && About(latest, view.Current) is not null && latest.Envelope.Actor.StartsWith("human:", StringComparison.Ordinal);
        return new MapPublishFacts(HeldVersion: null, default, TimeSpan.Zero, view.Current, [.. repos], reason, byHand);
    }

    // The event's payload, when it is about <version>; the ledger keeps the latest of the type, which may be an older version's.
    private static JsonElement? About(LedgerEvent recorded, int? version)
    {
        if (version is null)
        {
            return null;
        }

        using var document = JsonDocument.Parse(recorded.Envelope.Payload);
        var root = document.RootElement.Clone();
        return root.TryGetProperty("version", out var number) && number.TryGetInt32(out var n) && n == version ? root : null;
    }

    private static string? Text(JsonElement payload, string name) => payload.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
