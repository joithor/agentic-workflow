using System.Diagnostics.CodeAnalysis;
using Aw.Kernel.Ids;

namespace Aw.Sindri.Tests.Catalog;

/// <summary>A typed id that accepts any text, so CliForm.Fill's own value check is what refuses a line-breaking id.</summary>
/// <param name="Value">The text.</param>
public readonly record struct LooseId(string Value) : ITypedId<LooseId, string>
{
    public static bool TryFrom(string value, out LooseId id)
    {
        id = new LooseId(value);
        return true;
    }

    public static LooseId Parse(string s, IFormatProvider? provider) => new(s);

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out LooseId result)
    {
        result = new LooseId(s ?? string.Empty);
        return s is not null;
    }
}
