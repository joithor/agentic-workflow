using Aw.Evidence.Core.Guard;
using Aw.Evidence.Features.CheckProvenance;
using Aw.Evidence.Testing;

namespace Aw.Evidence.Tests.Stack;

public sealed class CheckProvenanceTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task CheckProvenance_is_seeded_under_the_configured_seed_policy()
    {
        var query = new FakeProvenanceQuery();
        query.Emails.Add("seed-1@seed.test");
        query.Emails.Add("staff@seed.test");
        var settings = new FakeSecrets(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["UI_EVIDENCE_SEED_DOMAINS"] = "seed.test",
            ["UI_EVIDENCE_SEED_EMAIL_PATTERN"] = "seed-%@seed.test",
        });

        var result = await new CheckProvenanceService(query, settings).Handle(new CheckProvenanceRequest(), CancellationToken.None);

        Assert.Equal(DbProvenance.Seeded, result.Value.Provenance);
        Assert.Equal(["seed-%@seed.test"], query.Patterns, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task CheckProvenance_is_unknown_with_the_reason_when_the_query_fails()
    {
        var query = new FakeProvenanceQuery { Failure = "docker: no such container" };

        var result = await new CheckProvenanceService(query, new FakeSecrets(new Dictionary<string, string>(StringComparer.Ordinal))).Handle(new CheckProvenanceRequest(), CancellationToken.None);

        Assert.Equal((DbProvenance.Unknown, "docker: no such container"), (result.Value.Provenance, result.Value.Reason));
    }
}
