using Aw.Sindri.Core.Commands;
using Aw.Sindri.Host.Catalog;

namespace Aw.Sindri.Tests.Catalog;

/// <summary>
/// The engine's catalog rows (engine spec §6.2, §6.3, §10 'Every_engine_variant_has_a_catalog_row'; UI spec §7, E-9,
/// E-12): every engine command and variant is listed with its class and UI status, and protected rows are terminal-bound.
/// Protected rows read 'pending:&lt;phase&gt;' until their endpoint is mapped, then 'cli-only:typed-confirmation'.
/// </summary>
public sealed class EngineRowTests
{
    [Fact, Trait("Lane", "unit")]
    public void Every_engine_variant_has_a_catalog_row()
    {
        var rows = new CommandCatalog().Rows.ToDictionary(r => r.Action.Value, StringComparer.Ordinal);

        Assert.All(_expected, e =>
        {
            Assert.True(rows.TryGetValue(e.Action, out var row), $"{e.Action} has no catalog row");
            Assert.Equal((e.Action, e.Class, e.Ui, e.Class == ActionClass.Protected || e.Action == "IssueChallenge"), (e.Action, row.Class, row.Ui, row.NeedsTerminal));
        });
    }

    private static readonly (string Action, ActionClass Class, string Ui)[] _expected =
    [
        ("RegisterRecipe", ActionClass.Desk, "pending:WF-P2"),
        ("ApproveRecipe", ActionClass.Protected, "pending:WF-P2"),
        ("RevokeRecipe", ActionClass.Protected, "pending:WF-P2"),
        ("IssueChallenge", ActionClass.Issue, "pending:WF-P2"),
        ("StartInstance", ActionClass.Desk, "pending:WF-P4"),
        ("IssueNonce", ActionClass.Issue, "pending:WF-P4"),
        ("ApplyCommand:answer-text", ActionClass.Desk, "pending:WF-P4"),
        ("ApplyCommand:answer-resume", ActionClass.Desk, "pending:WF-P4"),
        ("ApplyCommand:answer-reject", ActionClass.Desk, "pending:WF-P4"),
        ("ApplyCommand:answer-grant", ActionClass.Protected, "pending:WF-P4"),
        ("ApplyCommand:answer-grant-tokens", ActionClass.Protected, "pending:WF-P4"),
        ("ApplyCommand:answer-accept", ActionClass.Protected, "pending:WF-P4"),
        ("ApplyCommand:takeover", ActionClass.Pause, "pending:WF-P5"),
        ("ApplyCommand:handback", ActionClass.Desk, "pending:WF-P5"),
        ("ApplyCommand:cancel", ActionClass.Desk, "pending:WF-P4"),
        ("CompleteStep", ActionClass.Desk, "pending:WF-P4"),
    ];
}
