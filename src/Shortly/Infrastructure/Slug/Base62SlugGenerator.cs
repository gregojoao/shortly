using System.Security.Cryptography;
using Shortly.Application.Ports;

namespace Shortly.Infrastructure.Slug;

public sealed class Base62SlugGenerator : ISlugGenerator
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";

    public string Generate(int length)
    {
        if (length < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(length), length, "Slug length must be positive.");
        }

        Span<byte> buffer = stackalloc byte[length];
        RandomNumberGenerator.Fill(buffer);

        Span<char> chars = stackalloc char[length];
        for (var i = 0; i < length; i++)
        {
            chars[i] = Alphabet[buffer[i] % Alphabet.Length];
        }

        return new string(chars);
    }
}
