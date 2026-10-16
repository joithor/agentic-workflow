namespace Aw.Sindri.Features.Commands;

/// <summary>A request whose catalog row serves several commands and so names, in an authorization refusal, the one command it asked for.</summary>
internal interface IRefusalCli
{
    /// <summary>The CLI form to name instead of the row's, or null to name the row's.</summary>
    string? RefusalCli { get; }
}
