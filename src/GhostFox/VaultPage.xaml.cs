using GhostFox.Core.Services;

namespace GhostFox;

/// <summary>Менеджер паролей (Модуль 2): разблокировка мастер-паролем, CRUD, копирование, генератор.</summary>
public partial class VaultPage : ContentPage
{
    private readonly PasswordVault _vault;

    public VaultPage(PasswordVault vault)
    {
        InitializeComponent();
        _vault = vault;
        RefreshState();
    }

    private void RefreshState()
    {
        var unlocked = _vault.IsUnlocked;
        VaultContent.IsVisible = unlocked;
        LockButton.IsVisible = unlocked;
        UnlockButton.IsVisible = !unlocked;
        VaultStatusLabel.Text = unlocked
            ? $"Unlocked — {_vault.Entries.Count} entries. Autofill is active on matching sites."
            : _vault.Exists ? "Locked (vault exists)" : "No vault yet — set a master password to create one";
        EntriesView.ItemsSource = _vault.Entries.ToList();
    }

    private async void OnUnlockClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(MasterPasswordEntry.Text))
        {
            await DisplayAlert("Vault", "Enter a master password", "OK");
            return;
        }
        try
        {
            await _vault.UnlockAsync(MasterPasswordEntry.Text);
            MasterPasswordEntry.Text = string.Empty;
            RefreshState();
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            await DisplayAlert("Vault", "Wrong master password", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Vault", ex.Message, "OK");
        }
    }

    private void OnLockClicked(object sender, EventArgs e)
    {
        _vault.Lock();
        RefreshState();
    }

    private async void OnAddClicked(object sender, EventArgs e)
    {
        var site = await DisplayPromptAsync("Add password", "Site URL (e.g. https://example.com)");
        if (string.IsNullOrWhiteSpace(site)) return;
        var user = await DisplayPromptAsync("Add password", "Username / email");
        if (user == null) return;
        var pass = await DisplayPromptAsync("Add password", "Password (or cancel and use Generate)");
        if (string.IsNullOrWhiteSpace(pass)) pass = PasswordGenerator.Generate();
        await _vault.AddOrUpdateAsync(new PasswordVault.Entry(site.Trim(), user.Trim(), pass));
        RefreshState();
    }

    private async void OnCopyClicked(object sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: PasswordVault.Entry entry })
        {
            await Microsoft.Maui.ApplicationModel.DataTransfer.Clipboard.Default.SetTextAsync(entry.Password);
            await DisplayAlert("Vault", "Password copied to clipboard", "OK");
        }
    }

    private async void OnDeleteClicked(object sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: PasswordVault.Entry entry } &&
            await DisplayAlert("Vault", $"Delete password for {entry.Username} @ {entry.Site}?", "Delete", "Cancel"))
        {
            await _vault.RemoveAsync(entry.Site, entry.Username);
            RefreshState();
        }
    }

    private void OnGenerateClicked(object sender, EventArgs e) =>
        GeneratedLabel.Text = PasswordGenerator.Generate(24);

    private async void OnCloseClicked(object sender, EventArgs e) => await Navigation.PopModalAsync();
}
