namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>What the API answers a command it applied (the result words and the nonce are in the body too; the app reads the instance again instead).</summary>
/// <param name="AlreadyApplied">True when the same command had already been applied, so nothing new happened.</param>
internal sealed record CommandApplied(bool AlreadyApplied);
