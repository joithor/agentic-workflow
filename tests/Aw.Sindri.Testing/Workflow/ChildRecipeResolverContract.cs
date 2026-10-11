using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Validation;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>
/// What every <see cref="IChildRecipeResolver"/> does: the fake and each adapter (the same-directory reader, the
/// registered recipes) bind it with a resolver that holds <see cref="Known"/> and nothing else.
/// </summary>
public abstract class ChildRecipeResolverContract
{
    /// <summary>The child recipe the resolver under test holds.</summary>
    protected static ChildRecipe Known { get; } = new(ARecipe.Minimal(RecipeId.From("workflow.child")), RecipeHash.From("sha256:" + new string('c', 64)));

    /// <summary>A resolver that holds exactly <see cref="Known"/>.</summary>
    /// <returns>The resolver under test.</returns>
    protected abstract IChildRecipeResolver CreateWithKnown();

    /// <summary>A recipe the resolver holds resolves to that recipe and its hash.</summary>
    public void A_known_child_resolves_with_its_hash()
    {
        var found = CreateWithKnown().Resolve(Known.Recipe.Id);

        Expect.True(found is not null);
        Expect.Equal(Known.Recipe.Id, found!.Recipe.Id);   // why: checked non-null on the line above
        Expect.Equal(Known.Hash, found.Hash);
    }

    /// <summary>A recipe the resolver doesn't hold is null, never an exception.</summary>
    public void An_unknown_child_is_null() =>
        Expect.True(CreateWithKnown().Resolve(RecipeId.From("workflow.nowhere")) is null);
}
