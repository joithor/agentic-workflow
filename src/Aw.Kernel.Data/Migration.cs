using System.Security.Cryptography;
using System.Text;

namespace Aw.Kernel.Data;

/// <summary>One numbered schema change. Versions start at 1 and have no gaps; applied migrations never change.</summary>
/// <param name="Version">The version this migration takes the schema to.</param>
/// <param name="Name">A short name, from the file name after the number.</param>
/// <param name="Sql">The SQL, one or more statements.</param>
public sealed record Migration(int Version, string Name, string Sql)
{
    /// <summary>
    /// SHA-256 (lowercase hex) of the SQL as UTF-8, without a leading BOM and with CRLF or lone CR turned into LF,
    /// so a checkout's line-ending setting or a file loader never looks like an edit. Recorded when applied and checked on every run (AW-DATA-006 on drift).
    /// </summary>
    public string Checksum => Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(Normalize(Sql))));

    // A leading BOM and CRLF or lone CR line endings never count as an edit, whichever loader read the text.
    private static string Normalize(string sql) =>
        sql.TrimStart('\uFEFF').Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
}
