using Aw.Kernel.Ids;
using Xunit;

namespace Aw.Evidence.Testing;

/// <summary>The IIdGenerator contract: every id is a real (non-empty) Guid, and no two are alike.</summary>
public abstract class IdGeneratorContract
{
    /// <summary>Creates the generator under test.</summary>
    /// <returns>The generator.</returns>
    protected abstract IIdGenerator Create();

    /// <summary>A hundred ids are all non-empty and all different.</summary>
    [Fact]
    public void Ids_are_never_empty_and_never_repeat()
    {
        var ids = Create();

        Guid[] made = [.. Enumerable.Range(0, 100).Select(_ => ids.NewId())];

        Expect.That(made.All(static id => id != Guid.Empty), "no id is empty");
        Expect.That(new HashSet<Guid>(made).Count == made.Length, "no two ids are alike");
    }
}
