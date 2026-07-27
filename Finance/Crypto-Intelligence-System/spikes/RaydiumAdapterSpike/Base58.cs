using System.Numerics;

namespace RaydiumAdapterSpike;

internal static class Base58
{
    private const string Alphabet =
        "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";

    public static byte[] Decode(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length == 0)
        {
            return [];
        }

        var number = BigInteger.Zero;
        foreach (var character in value)
        {
            var digit = Alphabet.IndexOf(character);
            if (digit < 0)
            {
                throw new FormatException($"Invalid Base58 character '{character}'.");
            }

            number = number * 58 + digit;
        }

        var bytes = number.ToByteArray(isUnsigned: true, isBigEndian: true);
        var leadingZeros = value.TakeWhile(character => character == '1').Count();

        if (leadingZeros == 0)
        {
            return bytes;
        }

        var result = new byte[leadingZeros + bytes.Length];
        bytes.CopyTo(result, leadingZeros);
        return result;
    }
}
