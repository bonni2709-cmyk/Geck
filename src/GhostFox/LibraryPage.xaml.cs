using GhostFox.Core.Services;

namespace GhostFox;

/// <summary>Библиотека (Модуль 3): закладки и история, открытие в новой вкладке, удаление/очистка.</summary>
public partial class LibraryPage : ContentPage
{
    private readonly BrowserDataService _data;
    private readonly Action<string> _openUrl;

    public sealed record LibraryRow(string Title, string Url);

    public LibraryPage(BrowserDataService data, Action<string> openUrl)
    {
        InitializeComponent();
        _data = data;
        _openUrl = openUrl;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        var bookmarks = await _data.LoadBookmarksAsync();
        BookmarksView.ItemsSource = bookmarks.Select(b => new LibraryRow(
            string.IsNullOrWhiteSpace(b.Title) ? b.Url : b.Title, b.Url)).ToList();
        var history = await _data.LoadHistoryAsync();
        HistoryView.ItemsSource = history.OrderByDescending(h => h.VisitedAt)
            .Select(h => new LibraryRow(string.IsNullOrWhiteSpace(h.Title) ? h.Url : h.Title, h.Url)).ToList();
    }

    private async void OnItemSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is LibraryRow row)
        {
            await Navigation.PopModalAsync();
            _openUrl(row.Url);
        }
    }

    private async void OnDeleteBookmarkClicked(object sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: string url })
        {
            var bookmarks = await _data.LoadBookmarksAsync();
            bookmarks.RemoveAll(b => b.Url == url);
            await _data.SaveBookmarksAsync(bookmarks);
            await RefreshAsync();
        }
    }

    private async void OnClearHistoryClicked(object sender, EventArgs e)
    {
        await _data.SaveHistoryAsync(new List<Core.Models.HistoryEntry>());
        await RefreshAsync();
    }

    private async void OnCloseClicked(object sender, EventArgs e) => await Navigation.PopModalAsync();
}
