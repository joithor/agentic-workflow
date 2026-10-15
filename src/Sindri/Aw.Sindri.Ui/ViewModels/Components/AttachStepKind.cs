namespace Aw.Sindri.Ui.ViewModels.Components;

/// <summary>What kind of Step the row is on; it decides the main action.</summary>
internal enum AttachStepKind
{
    /// <summary>A live agent Step with a terminal session.</summary>
    Agent,

    /// <summary>A tool Step: no terminal.</summary>
    Tool,

    /// <summary>A model Step: no terminal.</summary>
    Model,
}
