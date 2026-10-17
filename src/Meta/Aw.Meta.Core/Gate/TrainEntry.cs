namespace Aw.Meta.Core.Gate;

/// <summary>One pull request in a train.</summary>
/// <param name="Position">Its place in the train (1 is gated on origin/main); 0 for a pull request left out before it was stacked.</param>
/// <param name="Pr">The pull request number.</param>
/// <param name="Base">What its gate is built on, such as "main" or "main+#12,#13".</param>
/// <param name="State">How it fared.</param>
/// <param name="Note">The reason, for a state that has one.</param>
public sealed record TrainEntry(int Position, int Pr, string Base, TrainState State, string Note = "");
