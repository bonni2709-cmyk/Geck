using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GhostFox.Core.Services;

/// <summary>
/// Модуль 2: зашифрованное хранилище паролей.
/// AES-256-GCM, ключ = PBKDF2-HMAC-SHA256(masterPassword, salt, 210k итераций).
/// Формат файла: JSON { salt, nonce, cipher } в base64. Мастер-пароль нигде не хранится.
/// </summary>
public sealed class PasswordVault
{
    public sealed record Entry(string Site, string Username, string Password);

    private sealed class VaultFile
    {
        public string Salt { get; set; } = string.Empty;
        public string Nonce { get; set; } = string.Empty;
        public string Cipher { get; set; } = string.Empty;
    }

    private readonly string _filePath;
    private byte[]? _key;
    private List<Entry> _entries = new();

    public PasswordVault(string filePath) => _filePath = filePath;
    public bool IsUnlocked => _key != null;
    public IReadOnlyList<Entry> Entries => _entries;

    public bool Exists => File.Exists(_filePath);

    /// <summary>Создать новый сейф или открыть существующий мастер-паролем.</summary>
    public async Task UnlockAsync(string masterPassword, CancellationToken ct = default)
    {
        if (!Exists)
        {
            var salt = RandomNumberGenerator.GetBytes(16);
            _key = DeriveKey(masterPassword, salt);
            _entries = new List<Entry>();
            await PersistAsync(salt, ct).ConfigureAwait(false);
            return;
        }

        var file = JsonSerializer.Deserialize<VaultFile>(await File.ReadAllTextAsync(_filePath, ct).ConfigureAwait(false))
                   ?? throw new InvalidOperationException("Vault file is corrupted.");
        var saltBytes = Convert.FromBase64String(file.Salt);
        var key = DeriveKey(masterPassword, saltBytes);
        var cipher = Convert.FromBase64String(file.Cipher);
        var nonce = Convert.FromBase64String(file.Nonce);
        var plain = new byte[cipher.Length - 16];
        var tag = cipher[^16..];
        var body = cipher[..^16];
        using var aes = new AesGcm(key, 16);
        aes.Decrypt(nonce, body, tag, plain); // выбросит CryptographicException при неверном пароле
        _key = key;
        _entries = JsonSerializer.Deserialize<List<Entry>>(Encoding.UTF8.GetString(plain)) ?? new List<Entry>();
    }

    public async Task AddOrUpdateAsync(Entry entry, CancellationToken ct = default)
    {
        EnsureUnlocked();
        _entries.RemoveAll(e => e.Site == entry.Site && e.Username == entry.Username);
        _entries.Add(entry);
        var salt = Convert.FromBase64String(
            (JsonSerializer.Deserialize<VaultFile>(await File.ReadAllTextAsync(_filePath, ct).ConfigureAwait(false))!).Salt);
        await PersistAsync(salt, ct).ConfigureAwait(false);
    }

    public async Task<bool> RemoveAsync(string site, string username, CancellationToken ct = default)
    {
        EnsureUnlocked();
        var removed = _entries.RemoveAll(e => e.Site == site && e.Username == username) > 0;
        if (!removed) return false;
        var salt = Convert.FromBase64String(
            (JsonSerializer.Deserialize<VaultFile>(await File.ReadAllTextAsync(_filePath, ct).ConfigureAwait(false))!).Salt);
        await PersistAsync(salt, ct).ConfigureAwait(false);
        return true;
    }

    public void Lock() { if (_key != null) CryptographicOperations.ZeroMemory(_key); _key = null; _entries = new(); }

    private async Task PersistAsync(byte[] salt, CancellationToken ct)
    {
        EnsureUnlocked();
        var plain = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(_entries));
        var nonce = RandomNumberGenerator.GetBytes(12);
        var body = new byte[plain.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(_key!, 16);
        aes.Encrypt(nonce, plain, body, tag);
        var file = new VaultFile
        {
            Salt = Convert.ToBase64String(salt),
            Nonce = Convert.ToBase64String(nonce),
            Cipher = Convert.ToBase64String(body.Concat(tag).ToArray())
        };
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        await File.WriteAllTextAsync(_filePath, JsonSerializer.Serialize(file), ct).ConfigureAwait(false);
    }

    private static byte[] DeriveKey(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, 210_000, HashAlgorithmName.SHA256, 32);

    private void EnsureUnlocked() { if (_key == null) throw new InvalidOperationException("Vault is locked."); }
}
