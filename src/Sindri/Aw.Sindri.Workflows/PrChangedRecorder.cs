using System.Security.Cryptography;
using System.Text;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// Records a 'pr.changed' (PR babysitter spec §6.1) in the ledger as the producer 'service:pr-watch': one event per item, since the event's route is 'item' and every live instance of the item gets it.
/// The outcome and 'fact:prEvent' travel in the payload ('item.routed''s shape); the fact is stored first. The idempotency key is made of the item, the pull request and what happened, so the
/// same change found again after a failed save of 'pr-watch.json' is the same row.
/// </summary>
/// <param name="ledger">The ledger.</param>
/// <param name="events">Builds the entries.</param>
/// <param name="facts">Stores 'fact:prEvent'.</param>
/// <param name="logger">The log.</param>
internal sealed partial class PrChangedRecorder(ILedgerStore ledger, EventFactory events, IFactStore facts, ILogger<PrChangedRecorder> logger)
{
    /// <summary>The producer of 'pr.changed'.</summary>
    public const string Actor = "service:pr-watch";

    private const string PrChanged = "pr.changed";

    private static readonly System.Text.Json.JsonSerializerOptions _json = AwJson.CreateOptions(WorkflowsJsonContext.Default);

    /// <summary>Records the change for each of its items.</summary>
    /// <param name="change">The change.</param>
    /// <param name="poll">The poll it was found in: its time, and the lease it is recorded under.</param>
    /// <param name="cancellationToken">Cancels the recording.</param>
    /// <returns>How many events were recorded, or the error that stopped it.</returns>
    public async ValueTask<Result<int>> Record(PrChange change, PrWatchPoll poll, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(change);
        ArgumentNullException.ThrowIfNull(poll);
        var fact = await facts.Put(AwJson.Serialize(new PrEventFact(change.Subject.Link, change.HeadSha, change.Changes, poll.Now), _json), cancellationToken).ConfigureAwait(false);
        if (fact.IsError)
        {
            return fact.Error;
        }

        var scope = new EventScope(Actor, events.Scope.Tenant, events.Scope.Ring);
        var payload = new RoutedItem(change.Outcome, new Dictionary<string, FactValue>(StringComparer.Ordinal) { ["fact:prEvent"] = fact.Value });
        foreach (var item in change.Subject.Items)
        {
            var key = "pr-watch:" + Hash($"{item}|{change.Subject.Link}|{change.Outcome}|{change.What}");
            var appended = await ledger.Append(events.Entry(PrChanged, payload, key, item, scope), poll.Run.Grant, cancellationToken).ConfigureAwait(false);
            if (appended.IsError)
            {
                return appended.Error;
            }

            Recorded(logger, item, change.Outcome);
        }

        return change.Subject.Items.Count;
    }

    private static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..32];

    [LoggerMessage(Level = LogLevel.Information, Message = "pr-watch: {Item} got pr.changed {Outcome}")]
    private static partial void Recorded(ILogger logger, string item, string outcome);
}
