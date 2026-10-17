namespace Aw.Sindri.Ui.Navigation;

/// <summary>A view model asks to open a recipe's sheet (the instance frame's Recipe link, a Workflows group head's Recipe button).</summary>
/// <param name="subject">The recipe link's subject: the id, and '@' and a hash when it names a version.</param>
internal sealed class RecipeRequestedEventArgs(string subject) : EventArgs
{
    /// <summary>The recipe link's subject.</summary>
    public string Subject { get; } = subject;
}
