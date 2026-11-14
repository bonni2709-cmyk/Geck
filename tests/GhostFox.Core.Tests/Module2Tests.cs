using GhostFox.Core.Models;
using GhostFox.Core.Services;
using Xunit;

namespace GhostFox.Core.Tests;

public class AdBlockTests
{
    private static AdBlockEngine EngineWith(string rules)
    {
        var e = new AdBlockEngine(seedBuiltInTrackers: false);
        e.LoadRules(rules);
        return e;
    }

    [Fact]
    public void DomainRuleBlocksSubdomainsButNotOthers()
    {
        var e = EngineWith("||tracker.example.com^");
        Assert.True(e.ShouldBlock("https://ads.tracker.example.com/pixel.gif"));
        Assert.True(e.ShouldBlock("https://tracker.example.com/x"));
        Assert.False(e.ShouldBlock("https://example.com/"));
        Assert.False(e.ShouldBlock("https://nottracker.example.com/"));
    }

    [Fact]
    public void ExceptionWins()
    {
        var e = EngineWith("||example.com^\n@@||good.example.com^");
        Assert.True(e.ShouldBlock("https://ads.example.com/"));
        Assert.False(e.ShouldBlock("https://good.example.com/"));
    }

    [Fact]
    public void PathRuleAndHostsFormat()
    {
        var e = EngineWith("||example.com/ads/*\n0.0.0.0 evil.test");
        Assert.True(e.ShouldBlock("https://example.com/ads/banner.js"));
        Assert.False(e.ShouldBlock("https://example.com/articles/1"));
        Assert.True(e.ShouldBlock("https://sub.evil.test/x"));
    }

    [Fact]
    public void CosmeticCssBuilt()
    {
        var e = EngineWith("##.global-ad\nexample.com##.popup");
        var css = e.BuildCosmeticCss("https://example.com/page");
        Assert.Contains(".global-ad", css);
        Assert.Contains(".popup", css);
        Assert.DoesNotContain(".popup", e.BuildCosmeticCss("https://other.org/"));
    }
}

public class GestureTests
{
    [Fact]
    public void LeftStrokeIsBack()
    {
        var points = Enumerable.Range(0, 11).Select(i => (200.0 - i * 20, 100.0)).ToList();
        Assert.Equal("L", GestureRecognizer.Recognize(points));
        Assert.Equal("back", GestureRecognizer.ResolveAction("L", GestureRecognizer.DefaultMap()));
    }

    [Fact]
    public void DownThenRightIsCloseTab()
    {
        var points = new List<(double, double)>();
        for (var i = 0; i <= 5; i++) points.Add((100.0, i * 30.0));
        for (var i = 1; i <= 5; i++) points.Add((100.0 + i * 30, 150.0));
        Assert.Equal("DR", GestureRecognizer.Recognize(points));
    }
}

public class DataTransferTests
{
    [Fact]
    public void NetscapeBookmarksRoundTrip()
    {
        var svc = new DataTransferService();
        var html = """
            <DL><p>
              <DT><H3>News</H3>
              <DL><p>
                <DT><A HREF="https://example.com/a" ADD_DATE="1">Example A</A>
              </DL><p>
              <DT><A HREF="https://mozilla.org/">Mozilla</A>
            </DL><p>
            """;
        var parsed = svc.ParseBookmarksHtml(html);
        Assert.Equal(2, parsed.Count);
        Assert.Equal("News", parsed[0].Folder);

        var exported = svc.ExportBookmarksHtml(parsed);
        var reparsed = svc.ParseBookmarksHtml(exported);
        Assert.Equal(2, reparsed.Count);
        Assert.Equal("https://mozilla.org/", reparsed[1].Url);
    }
}

public class CustomizationTests
{
    [Fact]
    public void PerSiteCssMatchesWildcard()
    {
        var rules = new List<SiteCssRule>
        {
            new() { HostPattern = "*.wikipedia.org", Css = "body{background:#000}" },
            new() { HostPattern = "example.com", Css = "x" },
        };
        var css = UserCssService.BuildCssFor("https://en.wikipedia.org/wiki/Fox", "global{}", rules);
        Assert.Contains("global{}", css);
        Assert.Contains("background:#000", css);
        Assert.DoesNotContain("\nx", css);
    }

    [Fact]
    public void PasswordGeneratorLength()
    {
        var p = PasswordGenerator.Generate(24);
        Assert.Equal(24, p.Length);
        Assert.NotEqual(p, PasswordGenerator.Generate(24));
    }

    [Fact]
    public void ThemesResolve()
    {
        Assert.Equal("Light", ThemeCatalog.Get("Light").Name);
        Assert.Equal("#FF0000", ThemeCatalog.Get("Dark", "#FF0000").Accent);
        Assert.Equal("Dark", ThemeCatalog.Get("nope").Name);
    }
}
