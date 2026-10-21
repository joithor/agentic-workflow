namespace Aw.Sindri.Features.GetItemScope;

/// <summary>The map version each lens read, 'v&lt;N&gt;'.</summary>
/// <param name="Code">The code lens.</param>
/// <param name="Product">The product lens.</param>
/// <param name="Related">The related lens.</param>
/// <param name="History">The history lens.</param>
public sealed record ScopeMapVersions(string Code, string Product, string Related, string History);
