namespace Aw.Sindri.Ui.ViewModels.Start;

/// <summary>Where the Start form's inline error belongs, so focus goes to the control it is about.</summary>
internal enum StartErrorTarget
{
    /// <summary>The API's refusal, or a failure with no control of its own: the error line.</summary>
    Summary,

    /// <summary>No recipe is chosen: the recipe picker.</summary>
    Recipe,

    /// <summary>No item is given: the item box.</summary>
    Item,

    /// <summary>An input that cannot be sent: that input's own control.</summary>
    Field,
}
