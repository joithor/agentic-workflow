namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>What is wrong with one entry of a choice list.</summary>
public enum ChoiceFaultKind
{
    /// <summary>The entry is a YAML mapping (an unquoted "ui: ...").</summary>
    Mapping = 1,

    /// <summary>The entry is a number, a flag, null or a list.</summary>
    NotText = 2,

    /// <summary>The text has no ": " after its outcome.</summary>
    NoSeparator = 3,

    /// <summary>The outcome isn't a name this build accepts.</summary>
    BadName = 4,

    /// <summary>The criterion is empty or longer than 400 characters.</summary>
    BadLength = 5,
}
