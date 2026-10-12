namespace Aw.Sindri.Core.Commands;

/// <summary>
/// Marks a request record as a command (API spec §2.1): served by an IService&lt;TCommand, TResult&gt; behind the command
/// chain (gate, audit, authorization, validation), mapped as a POST, and listed in the command catalog.
/// </summary>
/// <typeparam name="TResult">
/// What the command answers: CommandAccepted, or a record that carries it. S1's StopProcess and ApproveProfile are the
/// exception (C5): their responses carry only an added 'sequence'.
/// </typeparam>
public interface ICommand<TResult>;
