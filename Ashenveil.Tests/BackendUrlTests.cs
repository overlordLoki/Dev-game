using Ashenveil.Core.Config;

namespace Ashenveil.Tests;

/// <summary>A typed backend address, tidied into the stored form or turned down with a reason.</summary>
public class BackendUrlTests
{
    [Theory]
    [InlineData("http://127.0.0.1:8000", "http://127.0.0.1:8000")]
    [InlineData("http://127.0.0.1:8000/", "http://127.0.0.1:8000")]      // trailing slash dropped
    [InlineData("127.0.0.1:8000", "http://127.0.0.1:8000")]              // http:// assumed
    [InlineData("localhost:8000", "http://localhost:8000")]
    [InlineData("  http://localhost:8000  ", "http://localhost:8000")]   // spaces trimmed
    [InlineData("https://example.com", "https://example.com")]
    [InlineData("HTTP://Example.COM:8000", "http://example.com:8000")]
    [InlineData("http://example.com/ashenveil/", "http://example.com/ashenveil")]   // a path is kept
    public void A_good_address_is_tidied(string typed, string expected)
    {
        Assert.True(BackendUrl.TryNormalize(typed, out var url, out var error));
        Assert.Equal(expected, url);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("ftp://example.com")]
    [InlineData("http://")]
    [InlineData("http://exa mple.com")]
    [InlineData("http://example.com/?a=1")]
    [InlineData("http://example.com/#top")]
    public void A_bad_address_is_turned_down_with_a_reason(string? typed)
    {
        Assert.False(BackendUrl.TryNormalize(typed!, out var url, out var error));
        Assert.Null(url);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void An_empty_address_suggests_the_default()
    {
        BackendUrl.TryNormalize("", out _, out var error);

        Assert.Contains(GameSettings.DefaultBackendUrl, error);
    }
}
