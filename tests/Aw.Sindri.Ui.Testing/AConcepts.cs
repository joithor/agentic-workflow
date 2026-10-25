using System.Net;
using System.Text;
using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.Testing;

/// <summary>Builders for the concepts answer: a concept with sensible defaults, the states the server serves, and the answer as the route serves it.</summary>
internal static class AConcepts
{
    /// <summary>One concept.</summary>
    internal static ContextConcept Concept(string id, string name, string summary = "", int features = 1, int records = 0, int decisions = 0, int code = 0, string source = "model") =>
        new(id, name, summary, new ConceptSize(features, records, decisions, code), source);

    /// <summary>The synthetic concept of the unassigned nodes.</summary>
    internal static ConceptOther Other(int features = 0, int records = 0, int decisions = 0, int code = 0) => new("other", "Other", new ConceptSize(features, records, decisions, code));

    /// <summary>An ok answer.</summary>
    internal static ContextConceptsView Ok(IReadOnlyList<ContextConcept> concepts, ConceptOther? other = null, IReadOnlyList<ConceptLink>? links = null) =>
        new(1, "aw-split-panes", "ok", concepts, other ?? Other(), links);

    /// <summary>The three concepts most tests use: Self scheduling, Billing and Sync, linked Self scheduling to Billing, and a little unassigned.</summary>
    internal static ContextConceptsView Three() => Ok(
        [
            Concept("self-scheduling", "Self scheduling", "People book their own slots.", features: 4, records: 3, decisions: 2, code: 40),
            Concept("billing", "Billing", "Invoices and payments.", features: 2, records: 1, decisions: 1, code: 12),
            Concept("sync", "Sync", "Calendars stay in step.", features: 1, records: 0, decisions: 0, code: 7),
        ],
        Other(features: 1, code: 3),
        [new ConceptLink("self-scheduling", "billing")]);

    /// <summary>A state with no concepts to list: empty, building or unavailable.</summary>
    internal static ContextConceptsView Without(string state, string? code = null) => new(1, "aw-split-panes", state, Code: code);

    /// <summary>The answer as the route serves it.</summary>
    internal static RouteAnswer Served(ContextConceptsView view) => new(HttpStatusCode.OK, Encoding.UTF8.GetString(AwJson.Serialize(view, SindriApiClient.Json)));
}
