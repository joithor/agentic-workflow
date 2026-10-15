using Aw.Context.Core.Docs;
using Aw.Context.Core.Trust;
using Aw.Context.Testing.Engine;

namespace Aw.Context.Tests.Infrastructure;

[Trait("Lane", "integration")]
public sealed class SqliteDocIndexContractTests : DocIndexContract<IDocIndex>
{
    private readonly TempHome _home = new();

    protected override IDocIndex Create() => _home.Index;

    protected override Scrubbers ScrubbersFor(IReadOnlyList<ScrubPattern> patterns) => _home.Factory.For(patterns).Value;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _home.Dispose();
        }

        base.Dispose(disposing);
    }
}
