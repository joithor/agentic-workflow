using Aw.Evidence.Core;
using Aw.Kernel;

namespace Aw.Evidence.Tests;

public sealed class EvidenceErrorsTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_catalogue_is_valid() => Assert.True(ErrorCatalogue.Validate(EvidenceErrors.All).IsSuccess);
}
