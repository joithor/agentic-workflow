namespace Aw.Context.Core.Engine;

/// <summary>The OS user running the engine, recorded as the 'who' of the decisions a human makes at the terminal or in the app.</summary>
public interface ICurrentUser
{
    /// <summary>The user's name: not blank, one line.</summary>
    public string Name { get; }
}
