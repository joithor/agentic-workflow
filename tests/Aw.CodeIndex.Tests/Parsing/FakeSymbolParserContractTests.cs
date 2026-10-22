using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Ports;
using Aw.CodeIndex.Testing;

namespace Aw.CodeIndex.Tests.Parsing;

[Trait("Lane", "unit")]
public sealed class FakeSymbolParserContractTests : SymbolParserContract
{
    protected override ISymbolParser CreateParser() => new FakeSymbolParser().With("src/A.cs", new ParseOutcome([]));

    protected override string HandledPath => "src/A.cs";
}
