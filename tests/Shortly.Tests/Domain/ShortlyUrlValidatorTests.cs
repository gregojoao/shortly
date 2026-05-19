using FluentAssertions;
using Shortly.Domain;

namespace Shortly.Tests.Domain;

public sealed class ShortlyUrlValidatorTests
{
    private static readonly IReadOnlyCollection<string> DefaultSchemes =
        new[] { Uri.UriSchemeHttp, Uri.UriSchemeHttps };

    [Theory]
    [InlineData("https://example.com/foo")]
    [InlineData("http://promo.example.com/path?query=1")]
    public void IsAcceptable_returns_true_for_well_formed_http_urls(string raw)
    {
        var url = new Uri(raw, UriKind.Absolute);
        var ok = ShortlyUrlValidator.IsAcceptable(url, DefaultSchemes, Array.Empty<string>());
        ok.Should().BeTrue();
    }

    [Theory]
    [InlineData("ftp://example.com/x")]
    [InlineData("file:///etc/passwd")]
    [InlineData("javascript:alert(1)")]
    public void IsAcceptable_rejects_non_http_schemes(string raw)
    {
        var ok = Uri.TryCreate(raw, UriKind.Absolute, out var url)
            && ShortlyUrlValidator.IsAcceptable(url, DefaultSchemes, Array.Empty<string>());
        ok.Should().BeFalse();
    }

    [Fact]
    public void IsAcceptable_rejects_relative_urls()
    {
        var ok = ShortlyUrlValidator.IsAcceptable(
            new Uri("/relative", UriKind.Relative),
            DefaultSchemes,
            Array.Empty<string>());
        ok.Should().BeFalse();
    }

    [Fact]
    public void IsAcceptable_rejects_urls_above_max_length()
    {
        var huge = "https://example.com/" + new string('a', ShortlyUrlValidator.MaxUrlLength);
        var ok = ShortlyUrlValidator.IsAcceptable(
            new Uri(huge, UriKind.Absolute),
            DefaultSchemes,
            Array.Empty<string>());
        ok.Should().BeFalse();
    }

    [Theory]
    [InlineData("https://shopee.com.br/x", new[] { "shopee.com.br" }, true)]
    [InlineData("https://shopee.com.br/x", new[] { "*.shopee.com.br" }, false)]
    [InlineData("https://promo.shopee.com.br/x", new[] { "*.shopee.com.br" }, true)]
    [InlineData("https://evil.com/x", new[] { "shopee.com.br", "aliexpress.com" }, false)]
    public void IsAcceptable_honors_allowed_hosts(string raw, string[] allowedHosts, bool expected)
    {
        var url = new Uri(raw, UriKind.Absolute);
        var ok = ShortlyUrlValidator.IsAcceptable(url, DefaultSchemes, allowedHosts);
        ok.Should().Be(expected);
    }
}
