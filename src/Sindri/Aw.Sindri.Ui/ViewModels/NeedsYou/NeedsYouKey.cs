namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>The keys the Needs-you list takes (UI spec §8) once it has focus.</summary>
internal enum NeedsYouKey
{
    /// <summary>J: select the next row.</summary>
    J,

    /// <summary>K: select the previous row.</summary>
    K,

    /// <summary>A: run the row's primary action, when the app may.</summary>
    A,

    /// <summary>X: the option that turns a decision down.</summary>
    X,

    /// <summary>Y: copy the row's command.</summary>
    Y,

    /// <summary>Enter: open the row's sheet.</summary>
    Enter,

    /// <summary>Esc: close the sheet and return to the row.</summary>
    Escape,
}
