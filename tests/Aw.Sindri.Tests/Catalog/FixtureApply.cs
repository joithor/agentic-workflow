using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Tests.Catalog;

/// <summary>A classified command for UI spec E-12's fail-closed rule, shaped like the engine's ApplyCommand (whose rows the engine lane registers, G11).</summary>
/// <param name="Variant">The variant: 'answer-text' (desk) or 'answer-grant' (protected) in the fixture catalog.</param>
/// <param name="CarriesProtectedFields">True when the request carries a grant or accept field.</param>
public sealed record FixtureApply(string Variant, bool CarriesProtectedFields) : ICommand<CommandAccepted>, IClassifiedCommand;
