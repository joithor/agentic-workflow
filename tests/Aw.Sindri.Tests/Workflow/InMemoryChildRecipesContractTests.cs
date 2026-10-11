using Aw.Sindri.Core.Workflow.Validation;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow;

public sealed class InMemoryChildRecipesContractTests : ChildRecipeResolverContract
{
    [Fact, Trait("Lane", "unit")]
    public void Known_child_resolves() => A_known_child_resolves_with_its_hash();

    [Fact, Trait("Lane", "unit")]
    public void Unknown_child_is_null() => An_unknown_child_is_null();

    protected override IChildRecipeResolver CreateWithKnown() => new InMemoryChildRecipes().With(Known);
}
