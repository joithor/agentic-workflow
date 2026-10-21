using Microsoft.Data.Sqlite;

namespace Aw.CodeIndex.Infrastructure.Store;

/// <summary>One statement run many times in a transaction: the parameters are added once and set per row.</summary>
internal sealed class Stmt : IAsyncDisposable
{
    private readonly SqliteCommand _command;
    private readonly Dictionary<string, SqliteParameter> _parameters = new(StringComparer.Ordinal);

    internal Stmt(SqliteConnection connection, SqliteTransaction transaction, string sql, params string[] names)
    {
        _command = Sql.Command(connection, sql, transaction);
        foreach (var name in names)
        {
            var parameter = _command.CreateParameter();
            parameter.ParameterName = name;
            _ = _command.Parameters.Add(parameter);
            _parameters[name] = parameter;
        }
    }

    internal Stmt Set(string name, object? value)
    {
        _parameters[name].Value = value ?? DBNull.Value;
        return this;
    }

    internal async Task<int> Run(CancellationToken cancellationToken)
        => await _command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

    internal async Task<long> Insert(CancellationToken cancellationToken)
        => Convert.ToInt64(await _command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), System.Globalization.CultureInfo.InvariantCulture);

    public ValueTask DisposeAsync() => _command.DisposeAsync();
}
