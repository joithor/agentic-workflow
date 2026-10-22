using System.Security.Cryptography;

namespace Aw.CodeIndex.Infrastructure.Store;

/// <summary>The 26-character ULIDs that name build files: a millisecond clock then random bits, so names sort by creation and never repeat.</summary>
internal static class Ulid
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private const int RandomBytes = 10;
    private const int Length = 26;

    internal static string New(TimeProvider time)
    {
        var random = new byte[RandomBytes];
        RandomNumberGenerator.Fill(random);
        UInt128 value = (ulong)time.GetUtcNow().ToUnixTimeMilliseconds();
        foreach (var b in random)
        {
            value = (value << 8) | b;
        }

        var text = new char[Length];
        for (var i = Length - 1; i >= 0; i--)
        {
            text[i] = Alphabet[(int)(value & 31)];
            value >>= 5;
        }

        return new string(text);
    }

    internal static bool IsValid(string text)
        => text.Length == Length && text.All(c => Alphabet.Contains(c, StringComparison.Ordinal));
}
