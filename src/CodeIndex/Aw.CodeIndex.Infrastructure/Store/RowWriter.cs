using Aw.CodeIndex.Core.Model;
using Microsoft.Data.Sqlite;

namespace Aw.CodeIndex.Infrastructure.Store;

/// <summary>Inserts a file's rows (the file, its symbols, call sites, bands and bodies) with statements prepared once for a whole batch.</summary>
internal sealed class RowWriter : IAsyncDisposable
{
    private const string InsertFile = "INSERT INTO files(path, blob, size, lang, parsed) VALUES ($path, $blob, $size, $lang, $parsed)";

    private const string InsertSymbol = "INSERT INTO symbols(file, ref, name, qualified, kind, arity, start_line, end_line, exported, utility, signature, shape_hash, token_count, complexity, minhash) "
        + "VALUES ($file, $ref, $name, $qualified, $kind, $arity, $start, $end, $exported, $utility, $signature, $shape, $tokens, $complexity, $minhash) "
        + "RETURNING id";

    private const string InsertCallSite = "INSERT INTO call_sites(src, kind, name, arity, receiver, line) VALUES ($src, $kind, $name, $arity, $receiver, $line)";

    private const string InsertBand = "INSERT OR IGNORE INTO bands(key, symbol_id) VALUES ($key, $symbol)";

    private const string InsertBody = "INSERT INTO bodies(symbol_id, text) VALUES ($symbol, $text)";

    private readonly Stmt _file;
    private readonly Stmt _symbol;
    private readonly Stmt _callSite;
    private readonly Stmt _band;
    private readonly Stmt _body;

    internal RowWriter(SqliteConnection connection, SqliteTransaction transaction)
    {
        _file = new Stmt(connection, transaction, InsertFile, "$path", "$blob", "$size", "$lang", "$parsed");
        _symbol = new Stmt(connection, transaction, InsertSymbol, "$file", "$ref", "$name", "$qualified", "$kind", "$arity", "$start", "$end", "$exported", "$utility", "$signature", "$shape", "$tokens", "$complexity", "$minhash");
        _callSite = new Stmt(connection, transaction, InsertCallSite, "$src", "$kind", "$name", "$arity", "$receiver", "$line");
        _band = new Stmt(connection, transaction, InsertBand, "$key", "$symbol");
        _body = new Stmt(connection, transaction, InsertBody, "$symbol", "$text");
    }

    internal async Task Write(FileRecord file, CancellationToken cancellationToken)
    {
        _ = await _file.Set("$path", file.Path).Set("$blob", file.Blob).Set("$size", file.Size).Set("$lang", file.Language).Set("$parsed", file.Parsed ? 1 : 0).Run(cancellationToken).ConfigureAwait(false);
        foreach (var symbol in file.Symbols)
        {
            var id = await InsertSymbolRow(file.Path, symbol, cancellationToken).ConfigureAwait(false);
            foreach (var site in symbol.CallSites)
            {
                _ = await _callSite.Set("$src", id).Set("$kind", StoredNames.Text(site.Kind)).Set("$name", site.Name).Set("$arity", site.Arity).Set("$receiver", site.Receiver).Set("$line", site.Line).Run(cancellationToken).ConfigureAwait(false);
            }

            foreach (var key in symbol.BandKeys)
            {
                _ = await _band.Set("$key", key).Set("$symbol", id).Run(cancellationToken).ConfigureAwait(false);
            }

            if (symbol.Body is { } body)
            {
                _ = await _body.Set("$symbol", id).Set("$text", body).Run(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _file.DisposeAsync().ConfigureAwait(false);
        await _symbol.DisposeAsync().ConfigureAwait(false);
        await _callSite.DisposeAsync().ConfigureAwait(false);
        await _band.DisposeAsync().ConfigureAwait(false);
        await _body.DisposeAsync().ConfigureAwait(false);
    }

    private async Task<long> InsertSymbolRow(string path, SymbolRecord symbol, CancellationToken cancellationToken)
        => await _symbol
            .Set("$file", path)
            .Set("$ref", symbol.Ref.Value)
            .Set("$name", symbol.Name)
            .Set("$qualified", symbol.Qualified)
            .Set("$kind", StoredNames.Text(symbol.Kind))
            .Set("$arity", symbol.Arity)
            .Set("$start", symbol.StartLine)
            .Set("$end", symbol.EndLine)
            .Set("$exported", symbol.Exported ? 1 : 0)
            .Set("$utility", symbol.Utility ? 1 : 0)
            .Set("$signature", symbol.Signature)
            .Set("$shape", symbol.ShapeHash)
            .Set("$tokens", symbol.TokenCount)
            .Set("$complexity", symbol.Complexity)
            .Set("$minhash", symbol.MinHash.IsEmpty ? null : symbol.MinHash.ToArray())
            .Insert(cancellationToken).ConfigureAwait(false);
}
