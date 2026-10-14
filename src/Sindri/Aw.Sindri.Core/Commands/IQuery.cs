namespace Aw.Sindri.Core.Commands;

/// <summary>Marks a request record as a query (API spec §2.2): read-only, mapped as a GET (ARCH-CQRS-001).</summary>
/// <typeparam name="TView">The view it answers.</typeparam>
public interface IQuery<TView>;
