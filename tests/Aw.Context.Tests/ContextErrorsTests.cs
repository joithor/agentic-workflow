using Aw.Context.Core;
using Aw.Kernel;

namespace Aw.Context.Tests;

public sealed class ContextErrorsTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_catalogue_is_valid() => Assert.True(ErrorCatalogue.Validate(ContextErrors.All).IsSuccess);
}
