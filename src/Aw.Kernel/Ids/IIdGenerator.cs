namespace Aw.Kernel.Ids;

/// <summary>
/// The source of new ids. Code never calls 'Guid.NewGuid' itself (BannedSymbols.txt): the Host supplies the
/// real generator and tests a deterministic one (spec §4b.1, §4b.5). Typed ids come from
/// <see cref="IdGeneratorExtensions.New{TId}(IIdGenerator)"/> (decision R9, docs/csharp/typed-ids.md).
/// </summary>
public interface IIdGenerator
{
    /// <summary>Returns a new, unique id.</summary>
    /// <returns>The id.</returns>
    public Guid NewId();
}
