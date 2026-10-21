using Aw.Kernel.Testing;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Testing.Scoping;

namespace Aw.Sindri.Tests.Scoping;

/// <summary>The in-memory text shape honours the text-shape contract.</summary>
[Lane(TestLane.Unit)]
public sealed class InMemoryScopingTextShapeContractTests : ScopingTextShapeContract
{
    protected override IScopingTextShape Create() => new InMemoryScopingTextShape(["feature:links", "surface:dialog/share"]);
}
