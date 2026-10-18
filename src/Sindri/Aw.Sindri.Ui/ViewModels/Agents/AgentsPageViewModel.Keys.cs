namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>The Agents page's keys (<see cref="AgentsPageViewModel"/>, continued; T1 fills it).</summary>
internal sealed partial class AgentsPageViewModel
{
    /// <summary>The latest key the page was given; T1's key handling replaces it.</summary>
    internal string LastKey { get; private set; } = string.Empty;

    /// <summary>One single key on the page (J, K, Enter, O, Y, E); true when it did something.</summary>
    /// <param name="key">The key.</param>
    /// <returns>True when the key was handled.</returns>
    internal bool Press(string key)
    {
        LastKey = key;
        return false;
    }
}
