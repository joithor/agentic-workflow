using Aw.Context.Core.Trust;
using Aw.Context.Infrastructure.Scrubbing;
using Aw.Context.Testing.Engine;

namespace Aw.Context.Tests.Infrastructure;

[Trait("Lane", "unit")]
public sealed class RedactionScrubberFactoryContractTests : ScrubberFactoryContract<IScrubberFactory>
{
    protected override IScrubberFactory Create() => new RedactionScrubberFactory(TimeProvider.System);
}
