namespace Aw.Sindri.Host.Commands;

/// <summary>An API answer: the response read as a record, and the body exactly as received (what --json prints, API spec §9).</summary>
/// <param name="Value">The response record.</param>
/// <param name="Body">The response body's bytes.</param>
/// <typeparam name="T">The response record type.</typeparam>
internal sealed record ApiAnswer<T>(T Value, ReadOnlyMemory<byte> Body);
