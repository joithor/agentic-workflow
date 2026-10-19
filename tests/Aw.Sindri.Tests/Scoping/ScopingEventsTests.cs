using System.Reflection;
using Aw.Sindri.Core.Events;

namespace Aw.Sindri.Tests.Scoping;

/// <summary>The two ledger kinds of the scoping outcome metric (scoping spec §3, SC3): registered with ids and counts only.</summary>
public sealed class ScopingEventsTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData("scoping.approved", typeof(ScopingApproved), "scoping.approved:<packetHash>")]
    [InlineData("scoping.escape", typeof(ScopingEscape), "scoping.escape:<packetHash>:<station>:<reference>")]
    public void The_kind_is_in_the_catalog_with_its_payload_and_key_shape(string type, Type payload, string keyShape)
    {
        var entry = Assert.Single(EventCatalog.All, e => string.Equals(e.Type, type, StringComparison.Ordinal));

        Assert.True(entry.Public);
        Assert.Equal(payload, entry.PublicPayload);
        Assert.Equal(keyShape, entry.KeyShape);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(typeof(ScopingApproved))]
    [InlineData(typeof(ScopingEscape))]
    public void A_payload_holds_no_free_text_field(Type payload)
    {
        var names = payload.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(static p => p.Name).Where(static n => n != "EqualityContract");

        Assert.DoesNotContain(names, static n => n.Contains("Text", StringComparison.Ordinal) || n.Contains("Title", StringComparison.Ordinal) || n.Contains("Message", StringComparison.Ordinal));
    }
}
