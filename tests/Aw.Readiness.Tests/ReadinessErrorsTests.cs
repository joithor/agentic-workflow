using Aw.Kernel;
using Aw.Readiness.Core;

namespace Aw.Readiness.Tests;

public sealed class ReadinessErrorsTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_catalogue_is_valid() => Assert.True(ErrorCatalogue.Validate(ReadinessErrors.All).IsSuccess);
}
