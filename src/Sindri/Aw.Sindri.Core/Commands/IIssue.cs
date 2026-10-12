namespace Aw.Sindri.Core.Commands;

/// <summary>
/// Marks a request record as an issue (API spec §4.1, README X26): a POST that issues a one-time value and appends no
/// domain event. It runs the same chain as a command and is audited by 'command.handled'; only the carve-out routes may use it.
/// </summary>
/// <typeparam name="TResult">What it answers.</typeparam>
public interface IIssue<TResult>;
