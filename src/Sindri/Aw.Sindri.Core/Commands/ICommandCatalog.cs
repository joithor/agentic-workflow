namespace Aw.Sindri.Core.Commands;

/// <summary>The command catalog (API spec §3.3): one row per action id. The Host implements it, since routes and CLI forms are Host facts.</summary>
public interface ICommandCatalog
{
    /// <summary>Every row.</summary>
    public IReadOnlyList<CommandRow> Rows { get; }

    /// <summary>The rows served by <paramref name="commandType"/>: one for a plain command, one per variant for a classified one.</summary>
    /// <param name="commandType">The request record.</param>
    /// <returns>Its rows (empty when it has none).</returns>
    public IReadOnlyList<CommandRow> For(Type commandType);
}
