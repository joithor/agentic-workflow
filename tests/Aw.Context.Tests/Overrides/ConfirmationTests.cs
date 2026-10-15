using Aw.Context.Core.Overrides;
using Aw.Context.Core.Results;

namespace Aw.Context.Tests.Overrides;

public sealed class ConfirmationTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData(Provenance.Human, false, true)]
    [InlineData(Provenance.Deterministic, false, true)]
    [InlineData(Provenance.Model, false, false)]
    [InlineData(Provenance.Model, true, true)]
    public void A_person_or_the_extractor_stands_behind_it_or_an_accept_does(Provenance provenance, bool accepted, bool expected) =>
        Assert.Equal(expected, Confirmation.Of(provenance, accepted));
}
