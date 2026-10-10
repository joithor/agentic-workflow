namespace Aw.Kernel.Data.Tests;

[Trait("Lane", "integration")]
public sealed class EmbeddedMigrationsTests
{
    [Fact]
    public void Loads_numbered_sql_resources_in_version_order()
    {
        var loaded = EmbeddedMigrations.Load(typeof(EmbeddedMigrationsTests).Assembly, "Aw.Kernel.Data.Tests.Migrations.Good.");

        Assert.Equal([1, 2], loaded.Value.Select(m => m.Version));
        Assert.Equal(["items", "tags"], loaded.Value.Select(m => m.Name));
        Assert.StartsWith("CREATE TABLE items", loaded.Value[0].Sql, StringComparison.Ordinal);
    }

    [Fact]
    public void A_resource_without_a_version_number_is_refused()
    {
        var loaded = EmbeddedMigrations.Load(typeof(EmbeddedMigrationsTests).Assembly, "Aw.Kernel.Data.Tests.Migrations.Bad.");

        Assert.Equal(DataErrors.MigrationSequence.Code, loaded.Error.Code);
        Assert.Contains("items.sql", loaded.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void No_matching_resources_is_an_empty_set()
    {
        var loaded = EmbeddedMigrations.Load(typeof(EmbeddedMigrationsTests).Assembly, "Nothing.Here.");

        Assert.Empty(loaded.Value);
    }

    [Fact]
    public void The_catalogue_is_well_formed()
    {
        Assert.True(ErrorCatalogue.Validate(DataErrors.All).IsSuccess);
    }
}
