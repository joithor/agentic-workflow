using Aw.Context.Core.Symbols;
using Aw.Context.Testing.Symbols;

namespace Aw.Context.Tests.Ports;

/// <summary>The fake code symbol source passes the contract.</summary>
[Trait("Lane", "unit")]
public sealed class FakeCodeSymbolSourceContractTests : CodeSymbolSourceContract
{
    /// <inheritdoc/>
    protected override ICodeSymbolSource Create(IReadOnlyList<FakeIndexedRepo> repos) => new FakeCodeSymbolSource(repos);
}
