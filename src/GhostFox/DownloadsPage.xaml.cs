using GhostFox.Core.Models;
using GhostFox.Core.Services;

namespace GhostFox;

/// <summary>Менеджер загрузок (Модуль 3): список, прогресс, пауза (докачка — при повторном старте того же файла).</summary>
public partial class DownloadsPage : ContentPage
{
    private readonly DownloadService _downloads;

    public sealed record DownloadRow(Guid Id, string FileName, string StatusText, double Progress);

    public DownloadsPage(DownloadService downloads)
    {
        InitializeComponent();
        _downloads = downloads;
        _downloads.Changed += OnDownloadChanged;
        Refresh();
    }

    private void OnDownloadChanged(object? sender, DownloadItem item) =>
        MainThread.BeginInvokeOnMainThread(Refresh);

    private void Refresh()
    {
        DownloadsView.ItemsSource = _downloads.Items.Select(i => new DownloadRow(
            i.Id,
            Path.GetFileName(i.FilePath),
            $"{i.Status} — {i.BytesReceived / 1024} KB" + (i.TotalBytes is { } t ? $" / {t / 1024} KB" : "") + (i.Error != null ? $" ({i.Error})" : ""),
            i.Progress)).ToList();
    }

    private void OnPauseClicked(object sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: Guid id }) _downloads.Pause(id);
    }

    protected override void OnDisappearing()
    {
        _downloads.Changed -= OnDownloadChanged;
        base.OnDisappearing();
    }

    private async void OnCloseClicked(object sender, EventArgs e) => await Navigation.PopModalAsync();
}
