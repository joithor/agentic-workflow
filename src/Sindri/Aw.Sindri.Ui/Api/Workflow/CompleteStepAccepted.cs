namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>What the API answers a report it took (the instance after it and the nonce are in the body too; the app reads the instance again instead).</summary>
/// <param name="AlreadyApplied">True when the same report had already been applied, so nothing new happened.</param>
internal sealed record CompleteStepAccepted(bool AlreadyApplied);
