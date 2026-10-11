using Aw.Evidence.Core.Stack;
using Aw.Evidence.Testing;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Contracts;

[Lane(TestLane.Unit)]
public sealed class FakeProvenanceQueryContractTests : ProvenanceQueryContract
{
    protected override IProvenanceQuery Create(IReadOnlyList<string> emails)
    {
        var query = new FakeProvenanceQuery();
        foreach (var email in emails)
        {
            query.Emails.Add(email);
        }

        return query;
    }

    protected override IProvenanceQuery CreateFailing(string message) => new FakeProvenanceQuery { Failure = message };
}
