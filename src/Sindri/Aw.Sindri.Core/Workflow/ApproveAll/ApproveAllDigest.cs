using System.Security.Cryptography;
using System.Text;

namespace Aw.Sindri.Core.Workflow.ApproveAll;

/// <summary>
/// The digest an approve-all challenge is bound to (setup-once spec §5.3): sha256 over the ordered list of 'id@hash' and each recipe's capability ids (sorted),
/// served as its first six hex characters. Pure, so the engine and its fake agree on it. A plan with no row is '000000'.
/// </summary>
public static class ApproveAllDigest
{
    /// <summary>The digest of an empty plan.</summary>
    public const string Empty = "000000";

    /// <summary>The digest of <paramref name="rows"/>, in the order approval happens.</summary>
    /// <param name="rows">The plan's rows.</param>
    /// <returns>Six lower-case hex characters.</returns>
    public static string Of(IReadOnlyList<ApproveAllRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Count == 0)
        {
            return Empty;
        }

        var text = new StringBuilder();
        foreach (var row in rows)
        {
            _ = text.Append(row.Id.Value).Append('@').Append(row.Hash.Value).Append(':')
                .AppendJoin(',', row.Capabilities.Select(static c => c.Id.Value).Order(StringComparer.Ordinal)).Append('\n');
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())))[..6];
    }
}
