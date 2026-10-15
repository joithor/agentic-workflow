using Aw.Sindri.Core.Events;
using Aw.Sindri.Features.GetEventCatalog;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.GetEventCatalog;

public sealed class GetEventCatalogTests
{
    /// <summary>The public types no API view serves yet (the context, recipe and workflow views come with their phases); a phase that adds the view fills the row and drops the prefix.</summary>
    private static readonly string[] _withoutApiViewYet = ["context.", "recipe.", "workflow."];

    [Fact, Trait("Lane", "unit")]
    public async Task GetEventCatalog_lists_each_public_type_with_the_views_it_invalidates()
    {
        var service = new GetEventCatalogService();
        var request = A.GetEventCatalogRequest().Build();

        var result = await service.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var types = result.Value.Types;
        Assert.Equal(EventCatalog.All.Where(e => e.Public).Select(e => e.Type).Order(StringComparer.Ordinal), types.Select(t => t.Type), StringComparer.Ordinal);
        Assert.DoesNotContain(types, t => t.Type == EventTypes.CommandHandled);
        var created = Assert.Single(types, t => t.Type == EventTypes.ItemCreated);
        Assert.Equal("item.created:<itemId>", created.KeyShape);
        Assert.Equal([ApiViews.Queue, ApiViews.Items, ApiViews.Item, ApiViews.Timeline], created.Invalidates);
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_public_type_invalidates_at_least_one_view()
    {
        var empty = EventCatalog.All.Where(e => e.Public && e.Invalidates.Count == 0).Select(e => e.Type);

        Assert.All(empty, type => Assert.Contains(_withoutApiViewYet, prefix => type.StartsWith(prefix, StringComparison.Ordinal)));
    }

    [Fact, Trait("Lane", "unit")]
    public void Invalidates_names_only_ApiViews() =>
        Assert.All(EventCatalog.All.SelectMany(e => e.Invalidates), view => Assert.Contains(view, ApiViews.All, StringComparer.Ordinal));
}
