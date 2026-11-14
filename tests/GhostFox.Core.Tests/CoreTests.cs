using GhostFox.Core.Engines;
using GhostFox.Core.Models;
using GhostFox.Core.Services;
using Xunit;

namespace GhostFox.Core.Tests;

public class UrlNormalizerTests
{
    private static readonly SearchEngine Duck = new() { Name = "DuckDuckGo", UrlTemplate = "https://duckduckgo.com/?q={query}" };

    [Theory]
    [InlineData("example.com", "https://example.com/")]
    [InlineData("https://mozilla.org/en-US/", "https://mozilla.org/en-US/")]
    [InlineData("about:blank", "about:blank")]
    public void HostsAndUrls(string input, string expected) => Assert.Equal(expected, UrlNormalizer.Normalize(input, Duck));

    [Fact]
    public void FreeTextGoesToSearch() =>
        Assert.Equal("https://duckduckgo.com/?q=hello%20world", UrlNormalizer.Normalize("hello world", Duck));

    [Fact]
    public void DownloadsAreDetected() => Assert.True(UrlNormalizer.LooksLikeDownload("https://x.test/file.ZIP?dl=1".Replace("?dl=1", "")));
}

public class BlocklistTests
{
    [Theory]
    [InlineData("https://www.google-analytics.com/collect", true)]
    [InlineData("https://ads.doubleclick.net/x", true)]
    [InlineData("https://mozilla.org/", false)]
    [InlineData("not a url", false)]
    public void MatchesTrackerDomainsAndSubdomains(string url, bool expected) =>
        Assert.Equal(expected, new PrivacyBlocklist().ShouldBlock(url));
}

public class PasswordVaultTests
{
    [Fact]
    public async Task RoundTripAndWrongPassword()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".vault.json");
        try
        {
            var vault = new PasswordVault(path);
            await vault.UnlockAsync("correct horse battery staple");
            await vault.AddOrUpdateAsync(new PasswordVault.Entry("https://example.com", "fox", "s3cret"));
            vault.Lock();

            var reopened = new PasswordVault(path);
            await reopened.UnlockAsync("correct horse battery staple");
            Assert.Single(reopened.Entries);
            Assert.Equal("s3cret", reopened.Entries[0].Password);

            var wrong = new PasswordVault(path);
            await Assert.ThrowsAnyAsync<System.Security.Cryptography.CryptographicException>(
                () => wrong.UnlockAsync("wrong password"));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}

public class TabAndPlaceholderTests
{
    [Fact]
    public void TabsOpenCloseActivate()
    {
        var tabs = new TabManager();
        var a = tabs.OpenTab("https://a.test/");
        var b = tabs.OpenTab("https://b.test/");
        Assert.Equal(b.Id, tabs.ActiveTab!.Id);
        tabs.CloseTab(b.Id);
        Assert.Equal(a.Id, tabs.ActiveTab!.Id);
        var session = tabs.ToSession();
        var restored = new TabManager();
        restored.Restore(session);
        Assert.Single(restored.Tabs);
    }

    [Fact]
    public void PlaceholderEngineNavigates()
    {
        using var engine = new PlaceholderEngine();
        var seen = new List<string>();
        engine.Navigated += (_, e) => seen.Add(e.Url);
        engine.Navigate("https://a.test/");
        engine.Navigate("https://b.test/");
        engine.GoBack();
        Assert.True(engine.CanGoForward);
        Assert.Equal("https://a.test/", engine.CurrentUrl);
        Assert.Equal(3, seen.Count);
    }
}
