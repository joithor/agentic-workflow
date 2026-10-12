using System.Reflection;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Tests.Catalog;

/// <summary>The event catalog (API spec §3.4): every type Sindri records is listed once, and the audit row stays private.</summary>
public sealed class EventCatalogTests
{
    [Fact, Trait("Lane", "unit")]
    public void Every_event_type_is_in_the_event_catalog()
    {
        var constants = typeof(EventTypes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string)).Select(f => (string)f.GetRawConstantValue()!);
        var recorded = constants.Append(EventTypes.ServiceRun(ServiceName.From("heartbeat"))).ToList();
        var listed = EventCatalog.All.Select(e => e.Type).ToList();

        Assert.Equal(listed.Count, listed.Distinct(StringComparer.Ordinal).Count());
        Assert.All(recorded, type => Assert.Contains(type, listed, StringComparer.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void Command_handled_is_not_public()
    {
        var handled = Assert.Single(EventCatalog.All, e => e.Type == EventTypes.CommandHandled);

        Assert.False(handled.Public);
        Assert.Null(handled.PublicPayload);
        Assert.All(EventCatalog.All.Where(e => e.Public), e => Assert.NotNull(e.PublicPayload));
    }
}
