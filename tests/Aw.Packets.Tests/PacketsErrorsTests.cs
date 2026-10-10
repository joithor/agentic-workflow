using Aw.Kernel;
using Aw.Packets.Core;

namespace Aw.Packets.Tests;

public sealed class PacketsErrorsTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_catalogue_is_valid() => Assert.True(ErrorCatalogue.Validate(PacketsErrors.All).IsSuccess);
}
