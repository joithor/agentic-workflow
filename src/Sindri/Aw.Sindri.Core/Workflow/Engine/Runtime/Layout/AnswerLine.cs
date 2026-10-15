using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime.Layout;

/// <summary>The suggested answer line (E-23).</summary>
/// <param name="Kind">Which answer it takes.</param>
/// <param name="ActionId">The catalog action.</param>
/// <param name="Class">Desk or protected.</param>
/// <param name="Cli">The exact command the layout prints.</param>
/// <param name="AtTerminal">Whether the answer is given at a terminal.</param>
public sealed record AnswerLine(
    AnswerKind Kind,
    ActionId ActionId,
    ActionClass Class,
    string Cli,
    bool AtTerminal);
