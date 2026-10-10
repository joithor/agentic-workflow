// Must not compile: Vogen's analyzer bans default instances of value objects (VOG009).
using Spike.R9;

var probe = default(ItemId);
Console.WriteLine(probe);
