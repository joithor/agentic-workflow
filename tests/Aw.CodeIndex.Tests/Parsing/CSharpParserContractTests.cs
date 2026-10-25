using Aw.CodeIndex.Core.Ports;
using Aw.CodeIndex.Infrastructure.Parsing;
using Aw.CodeIndex.Testing;

namespace Aw.CodeIndex.Tests.Parsing;

[Trait("Lane", "unit")]
public sealed class CSharpParserContractTests : SymbolParserContract
{
    protected override ISymbolParser CreateParser() => new CSharpParser();

    protected override string HandledPath => "src/A.cs";
}
