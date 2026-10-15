namespace Aw.Sindri.Ui.Platform;

/// <summary>The messages a later launch sends the running instance (<see cref="ISingleInstance.Send"/>).</summary>
public static class InstanceMessages
{
    /// <summary>Bring the window forward; navigate nowhere.</summary>
    public const string Focus = "focus";

    /// <summary>The longest message a running instance reads (a deep link is far shorter); longer ones are dropped.</summary>
    public const int MaxLength = 512;
}
