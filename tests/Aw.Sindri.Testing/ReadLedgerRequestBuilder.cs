using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.ReadLedger;

namespace Aw.Sindri.Testing;

/// <summary>Builds a ReadLedgerRequest with deterministic defaults: from the start, every item and type.</summary>
public sealed class ReadLedgerRequestBuilder
{
    private long _after;
    private ItemRef? _item;
    private string? _type;
    private int _max = 100;

    /// <summary>Sets the cursor.</summary>
    /// <param name="value">The last sequence seen.</param>
    /// <returns>This builder.</returns>
    public ReadLedgerRequestBuilder After(long value)
    {
        _after = value;
        return this;
    }

    /// <summary>Sets the item filter.</summary>
    /// <param name="value">The id or alias.</param>
    /// <returns>This builder.</returns>
    public ReadLedgerRequestBuilder WithItem(ItemRef value)
    {
        _item = value;
        return this;
    }

    /// <summary>Sets the type glob list.</summary>
    /// <param name="value">The globs.</param>
    /// <returns>This builder.</returns>
    public ReadLedgerRequestBuilder WithType(string value)
    {
        _type = value;
        return this;
    }

    /// <summary>Sets the page size.</summary>
    /// <param name="value">The most events.</param>
    /// <returns>This builder.</returns>
    public ReadLedgerRequestBuilder WithMax(int value)
    {
        _max = value;
        return this;
    }

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public ReadLedgerRequest Build() => new(_after, _item, _type, _max);
}
