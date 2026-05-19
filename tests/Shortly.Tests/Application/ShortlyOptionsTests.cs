using FluentAssertions;
using Shortly.Application;

namespace Shortly.Tests.Application;

public sealed class ShortlyOptionsTests
{
    [Fact]
    public void Validate_succeeds_with_defaults_plus_BaseUrl()
    {
        var options = new ShortlyOptions { BaseUrl = new Uri("https://l.example.com") };

        var act = () => options.Validate();

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_throws_when_BaseUrl_is_missing()
    {
        var options = new ShortlyOptions();

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*BaseUrl*");
    }

    [Theory]
    [InlineData("ftp://example.com")]
    [InlineData("file:///c:/temp")]
    public void Validate_throws_when_BaseUrl_is_not_http(string raw)
    {
        var options = new ShortlyOptions { BaseUrl = new Uri(raw) };

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Validate_throws_when_SlugLength_is_below_minimum()
    {
        var options = new ShortlyOptions
        {
            BaseUrl = new Uri("https://l.example.com"),
            SlugLength = 1
        };

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*SlugLength*");
    }

    [Fact]
    public void Validate_throws_when_CacheTtl_is_zero_or_negative()
    {
        var options = new ShortlyOptions
        {
            BaseUrl = new Uri("https://l.example.com"),
            CacheTtl = TimeSpan.Zero
        };

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*CacheTtl*");
    }

    [Fact]
    public void Validate_throws_when_DefaultLinkTtl_is_zero()
    {
        var options = new ShortlyOptions
        {
            BaseUrl = new Uri("https://l.example.com"),
            DefaultLinkTtl = TimeSpan.Zero
        };

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*DefaultLinkTtl*");
    }

    [Fact]
    public void Validate_throws_when_AllowedSchemes_is_empty()
    {
        var options = new ShortlyOptions
        {
            BaseUrl = new Uri("https://l.example.com"),
            AllowedSchemes = Array.Empty<string>()
        };

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*AllowedSchemes*");
    }
}
