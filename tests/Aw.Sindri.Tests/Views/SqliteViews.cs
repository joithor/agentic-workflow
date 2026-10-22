using Aw.Kernel.Json;
using Aw.Sindri.Host;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Views;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Views;

/// <summary>The SQLite views store and reader over one views.db in a home directory (each over its own database object).</summary>
internal sealed class SqliteViews : IDisposable
{
    private readonly ViewsDatabase _writer;
    private readonly ViewsDatabase _reader;

    public SqliteViews(string home)
    {
        var settings = new SindriSettings(home, System.IO.Path.Combine(home, "profile.yaml"));
        var options = AwJson.CreateOptions(SindriJsonContext.Default);
        _writer = new ViewsDatabase(settings, new FakeTimeProvider());
        _reader = new ViewsDatabase(settings, new FakeTimeProvider());
        Store = new SqliteViewStore(_writer, options);
        Reader = new SqliteViewReader(_reader, options);
        Path = settings.ViewsPath;
        Database = _writer;
        Options = options;
    }

    public SqliteViewStore Store { get; }

    public SqliteViewReader Reader { get; }

    public string Path { get; }

    public ViewsDatabase Database { get; }

    public System.Text.Json.JsonSerializerOptions Options { get; }

    public void Dispose()
    {
        _writer.Dispose();
        _reader.Dispose();
    }
}
