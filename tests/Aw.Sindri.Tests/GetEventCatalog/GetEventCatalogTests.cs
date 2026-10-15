using Aw.Sindri.Core.Events;
using Aw.Sindri.Features.GetEventCatalog;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.GetEventCatalog;

public sealed class GetEventCatalogTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task GetEventCatalog_lists_each_public_type_that_maps_to_a_view_with_the_views_it_invalidates()
    {
        var service = new GetEventCatalogService();
        var request = A.GetEventCatalogRequest().Build();

        var result = await service.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var types = result.Value.Types;
        Assert.Equal(EventCatalog.All.Where(e => e.Public && e.Invalidates.Count > 0).Select(e => e.Type).Order(StringComparer.Ordinal), types.Select(t => t.Type), StringComparer.Ordinal);
        Assert.DoesNotContain(types, t => t.Type == EventTypes.CommandHandled);
        var created = Assert.Single(types, t => t.Type == EventTypes.ItemCreated);
        Assert.Equal("item.created:<itemId>", created.KeyShape);
        Assert.Equal([ApiViews.Queue, ApiViews.Items, ApiViews.Item, ApiViews.Timeline], created.Invalidates);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Every_served_type_invalidates_at_least_one_view_and_a_public_type_with_none_is_left_out()
    {
        var served = (await new GetEventCatalogService().Handle(A.GetEventCatalogRequest().Build(), CancellationToken.None)).Value.Types;
        var unmapped = EventCatalog.All.Where(e => e.Public && e.Invalidates.Count == 0).Select(e => e.Type).ToList();

        Assert.NotEmpty(unmapped);   // the context, recipe and workflow types have no API view yet
        Assert.All(served, t => Assert.NotEmpty(t.Invalidates));
        Assert.Empty(served.Select(t => t.Type).Intersect(unmapped, StringComparer.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void Invalidates_names_only_ApiViews() =>
        Assert.All(EventCatalog.All.SelectMany(e => e.Invalidates), view => Assert.Contains(view, ApiViews.All, StringComparer.Ordinal));
}
