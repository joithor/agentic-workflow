using Aw.BugFix.Core.Errors;
using Aw.Kernel;

namespace Aw.BugFix.Tests;

public sealed class BugFixErrorsTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_catalogue_is_valid() => Assert.True(ErrorCatalogue.Validate(BugFixErrors.All).IsSuccess);
}
