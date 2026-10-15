using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime.Layout;

/// <summary>One command the instance accepts (E-23).</summary>
/// <param name="Verb">The verb.</param>
/// <param name="ActionId">The catalog action.</param>
/// <param name="Class">Desk or protected.</param>
/// <param name="Cli">The CLI line.</param>
public sealed record CommandRow(
    string Verb,
    ActionId ActionId,
    ActionClass Class,
    string Cli);
