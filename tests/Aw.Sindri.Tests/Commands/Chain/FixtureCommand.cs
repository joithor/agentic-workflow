using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Tests.Commands.Chain;

/// <summary>A desk command for the chain tests: 'accept' appends one domain event, 'fail' answers a conflict, 'full' answers SND-LEDGER-002, 'block' waits for <see cref="FixtureLatch"/>.</summary>
/// <param name="Outcome">What the handler does; 'invalid' is refused by its validator.</param>
public sealed record FixtureCommand(string Outcome) : ICommand<CommandAccepted>;
