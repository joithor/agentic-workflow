using Aw.CodeIndex.Core;
using Aw.Kernel;

namespace Aw.CodeIndex.Tests;

public sealed class CodeIndexErrorsTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_catalogue_is_valid() => Assert.True(ErrorCatalogue.Validate(CodeIndexErrors.All).IsSuccess);
}
