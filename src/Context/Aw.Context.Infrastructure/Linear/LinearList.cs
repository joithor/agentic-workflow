using System.Text.Json.Nodes;

namespace Aw.Context.Infrastructure.Linear;

/// <summary>One list read: the operation, its filter, how many nodes are wanted at most, and how the connection is taken out of the operation's data.</summary>
/// <param name="Operation">The list operation ('Projects', 'DocumentHits', ...).</param>
/// <param name="Filter">Its filter, or null.</param>
/// <param name="Want">How many nodes are wanted at most.</param>
/// <param name="Pick">Takes the connection out of the operation's data.</param>
/// <typeparam name="TData">The operation's data record.</typeparam>
/// <typeparam name="TNode">The node record.</typeparam>
internal sealed record LinearList<TData, TNode>(string Operation, JsonObject? Filter, int Want, Func<TData, LinearConnection<TNode>> Pick);
