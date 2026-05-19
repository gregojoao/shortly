using FluentAssertions;
using Shortly.Domain;
using Shortly.Infrastructure.Slug;

namespace Shortly.Tests.Infrastructure.Slug;

public sealed class Base62SlugGeneratorTests
{
    [Theory]
    [InlineData(4)]
    [InlineData(7)]
    [InlineData(16)]
    public void Generate_returns_a_valid_slug_of_requested_length(int length)
    {
        var generator = new Base62SlugGenerator();

        var slug = generator.Generate(length);

        slug.Should().HaveLength(length);
        Shortly.Domain.Slug.IsValid(slug).Should().BeTrue();
    }

    [Fact]
    public void Generate_throws_when_length_is_zero()
    {
        var generator = new Base62SlugGenerator();

        var act = () => generator.Generate(0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Generate_avoids_visually_ambiguous_characters()
    {
        var generator = new Base62SlugGenerator();
        var forbidden = new[] { '0', 'O', '1', 'I', 'l' };

        for (var i = 0; i < 200; i++)
        {
            var slug = generator.Generate(10);
            slug.IndexOfAny(forbidden).Should().Be(-1, $"slug '{slug}' must not contain visually-ambiguous chars");
        }
    }

    [Fact]
    public void Generate_produces_high_entropy_output()
    {
        var generator = new Base62SlugGenerator();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < 500; i++)
        {
            seen.Add(generator.Generate(8)).Should().BeTrue("500 random 8-char slugs should not collide");
        }
    }
}
