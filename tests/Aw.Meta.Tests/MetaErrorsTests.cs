using Aw.Kernel;
using Aw.Meta.Core;

namespace Aw.Meta.Tests;

public sealed class MetaErrorsTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_catalogue_is_valid() => Assert.True(ErrorCatalogue.Validate(MetaErrors.All).IsSuccess);
}
