namespace Aw.Sindri.Core.Sessions;

/// <summary>The terminals host detection knows by process name (terminals.json).</summary>
public interface ITerminalNames
{
    /// <summary>Every terminal row, in file order.</summary>
    public IReadOnlyList<TerminalName> All { get; }
}
