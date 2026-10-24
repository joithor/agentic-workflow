using Aw.Context.Core.Engine;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Map;
using Aw.Kernel;

namespace Aw.Context.Features.SyncSources;

/// <summary>
/// Links every stored record again against the map that is current (spec §13), from the cleaned text each record kept: <see cref="RulesLinker"/> with the sync's own rules, so it is pure and
/// offline, and running it twice gives the same links. A record's old links are replaced (one into a node the map no longer has goes; one by issue key to a record not synced yet comes back,
/// the linker emits it), the record counts as linked under the new map version, and its decisions and extraction state are not touched. A record with no kept text (stored before the text was
/// kept) keeps its links until a sync fetches it. Runs as the engine itself, like the sync, so the links are the same whoever published the map.
/// </summary>
internal sealed class IntentRelinker(MapReader graphs, IIntentStore intents, TimeProvider clock)
{
    /// <summary>Links the stored records against the current map.</summary>
    /// <param name="scope">The profile's context section and repos of the call that published the map.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>How many records were linked again, or the reason nothing was (nothing is written then).</returns>
    public async ValueTask<Result<int>> Run(ContextScope scope, CancellationToken cancellationToken)
    {
        var settings = SettingsReader.Read(scope.Settings);
        if (settings.IsError)
        {
            return settings.Error;
        }

        var layer = await intents.Layer(cancellationToken).ConfigureAwait(false);
        if (layer.IsError)
        {
            return layer.Error;
        }

        var texts = await intents.Texts(cancellationToken).ConfigureAwait(false);
        if (texts.IsError)
        {
            return texts.Error;
        }

        var records = layer.Value.Records.Where(r => texts.Value.ContainsKey(r.Key.Value)).ToList();
        if (records.Count == 0)
        {
            return 0;   // nothing kept its text yet: the map isn't read
        }

        var engine = scope with { Caller = new ContextCaller(ContextCallerKind.Service, Repo: null, scope.Caller.Client) };
        var map = await graphs.ReadWithoutIntent(engine, draft: false, cancellationToken).ConfigureAwait(false);
        if (map.IsError)
        {
            return map.Error;
        }

        var teams = new HashSet<string>(settings.Value.LinearTeams, StringComparer.Ordinal);
        var links = records.SelectMany(r => RulesLinker.Link(r.Key, texts.Value[r.Key.Value], map.Value.Graph, layer.Value.Records, teams)).ToList();
        var done = await intents.Relink([.. records.Select(r => r.Key)], links, map.Value.Snapshot.Version.Number, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        return done.IsError ? done.Error : records.Count;
    }
}
