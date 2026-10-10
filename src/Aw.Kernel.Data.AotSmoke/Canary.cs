using System.Globalization;

namespace Aw.Kernel.Data.AotSmoke;

/// <summary>The canary's id type, row mapper and output.</summary>
internal static class Canary
{
    internal static void Line(string text) => Console.Out.Write(text + "\n");

    internal readonly record struct RunId(Guid Value);

    internal sealed class RunMapper : IRowMapper<string>
    {
        public string Map(Microsoft.Data.Sqlite.SqliteDataReader reader) =>
            SqliteRead.GetGuid(reader, 0).ToString("D", CultureInfo.InvariantCulture) + "|" + reader.GetString(1) + "|" + reader.GetString(2);
    }
}
