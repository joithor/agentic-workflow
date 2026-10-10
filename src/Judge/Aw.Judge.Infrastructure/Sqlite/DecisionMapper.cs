using Aw.Judge.Core.Decisions;
using Aw.Judge.Core.Ids;
using Aw.Kernel.Data;
using Microsoft.Data.Sqlite;

namespace Aw.Judge.Infrastructure.Sqlite;

/// <summary>Maps DecisionSql.Find's columns: the decision (0-13), then its detail row (14-19, null when absent).</summary>
internal sealed class DecisionMapper : IRowMapper<DecisionRecord>
{
    internal static readonly DecisionMapper Instance = new();

    public DecisionRecord Map(SqliteDataReader reader) => new()
    {
        Id = DecisionId.From(reader.GetString(0)),
        Ts = SqliteRead.GetTimestamp(reader, 1),
        Question = reader.GetString(2),
        ContentClass = reader.GetString(3),
        Provider = reader.GetString(4),
        Decision = reader.IsDBNull(5) ? null : reader.GetString(5),
        Confidence = reader.GetDouble(6),
        ReasonCode = reader.GetString(7),
        LatencyMs = reader.GetInt64(8),
        InputDigest = reader.GetString(9),
        UndoneAt = reader.IsDBNull(10) ? null : SqliteRead.GetTimestamp(reader, 10),
        ChainPosition = reader.GetInt32(11),
        Skipped = DecisionJson.Skipped(reader.GetString(12)),
        Outcome = DecisionText.Outcome(reader.GetString(13)),
        Details = reader.IsDBNull(14) ? null : Details(reader),
    };

    private static DecisionDetails Details(SqliteDataReader reader) => new()
    {
        InputJson = reader.GetString(15),
        Probabilities = DecisionJson.Probabilities(reader.IsDBNull(16) ? null : reader.GetString(16)),
        RulesOpinion = reader.IsDBNull(17) ? null : reader.GetString(17),
        Agreement = reader.IsDBNull(18) ? null : DecisionText.AgreementOf(reader.GetString(18)),
        SessionId = reader.IsDBNull(19) ? null : SessionId.From(reader.GetString(19)),
    };
}
