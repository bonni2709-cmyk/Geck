using GhostFox.Core.Models;
using GhostFox.Core.Services;
using Xunit;

namespace GhostFox.Core.Tests;

public class TranslationTests
{
    private sealed class FakeHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var json = "{\"responseData\":{\"translatedText\":\"Privet\"}}";
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            { Content = new StringContent(json) });
        }
    }

    [Fact]
    public void DetectsLanguages()
    {
        Assert.Equal("ru", TranslationService.DetectLanguage("Привет, как дела сегодня"));
        Assert.Equal("en", TranslationService.DetectLanguage("Hello world, this is a test"));
        Assert.Equal("ko", TranslationService.DetectLanguage("안녕하세요 세상"));
    }

    [Fact]
    public async Task TranslatesViaClient()
    {
        var svc = new TranslationService(new HttpClient(new FakeHandler()));
        var result = await svc.TranslateAsync("Hello", "ru", "en");
        Assert.Equal("Privet", result);
        // кэш: второй вызов не должен упасть и вернёт то же
        Assert.Equal("Privet", await svc.TranslateAsync("Hello", "ru", "en"));
    }

    [Fact]
    public void ChunksLongText()
    {
        var text = string.Join(". ", Enumerable.Range(0, 40).Select(i => "Sentence number " + i)) + ".";
        var chunks = TranslationService.Chunk(text);
        Assert.True(chunks.Count > 1);
        Assert.All(chunks, c => Assert.True(c.Length <= 460));
    }
}

public class PipTests
{
    [Fact]
    public void ParsesVideoInfoAndBuildsPage()
    {
        var json = "{\"source\":\"https://cdn.test/movie.mp4\",\"currentTime\":42.5,\"paused\":false,\"pageTitle\":\"Film\"}";
        var info = PipService.ParseInfo(json);
        Assert.NotNull(info);
        Assert.Equal(42.5, info!.CurrentTime);
        var html = PipService.BuildPipHtml(info);
        Assert.Contains("movie.mp4", html);
        Assert.Contains("42.5", html);
        Assert.Null(PipService.ParseInfo("{\"error\":\"streaming-mse\"}"));
        Assert.Equal("streaming-mse", PipService.ErrorOf("{\"error\":\"streaming-mse\"}"));
    }
}

public class NamedSessionTests
{
    [Fact]
    public async Task SaveListLoadDelete()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            var data = new BrowserDataService(new JsonStore(dir));
            var state = new SessionState();
            state.Tabs.Add(new BrowserTab { Url = "https://a.test/", Title = "A" });
            await data.SaveNamedSessionAsync("Work stuff", state);
            Assert.Contains("Work stuff", await data.ListNamedSessionsAsync());
            var loaded = await data.LoadNamedSessionAsync("Work stuff");
            Assert.Single(loaded.Tabs);
            await data.DeleteNamedSessionAsync("Work stuff");
            Assert.Empty(await data.ListNamedSessionsAsync());
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}
