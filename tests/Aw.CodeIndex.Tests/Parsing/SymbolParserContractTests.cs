using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Ports;
using Aw.CodeIndex.Infrastructure.Parsing;
using Aw.CodeIndex.Testing;

namespace Aw.CodeIndex.Tests.Parsing;

[Trait("Lane", "unit")]
public sealed class FakeSymbolParserContractTests : SymbolParserContract
{
    protected override ISymbolParser CreateParser() => new FakeSymbolParser().With("src/A.cs", new ParseOutcome([]));

    protected override string HandledPath => "src/A.cs";
}

[Trait("Lane", "unit")]
public sealed class CSharpParserContractTests : SymbolParserContract
{
    protected override ISymbolParser CreateParser() => new CSharpParser();

    protected override string HandledPath => "src/A.cs";
}
