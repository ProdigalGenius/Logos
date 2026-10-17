using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using SahaNet.Application.Identity.Abstractions;
using SahaNet.Infrastructure.Options;

namespace SahaNet.Infrastructure.Security;

/// <summary>
/// AES-256-GCM ile kolon şifreleme. Çıktı biçimi: "v1:" + Base64(nonce | şifreli metin | etiket).
/// Sürüm öneki ileride anahtar rotasyonuna olanak tanır.
/// </summary>
internal sealed class AesGcmPersonalDataProtector : IPersonalDataProtector, IDisposable
{
    private const string Prefix = "v1:";
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly AesGcm _aes;
    private readonly byte[] _lookupKey;

    public AesGcmPersonalDataProtector(IOptions<PersonalDataOptions> options)
    {
        var encryptionKey = Convert.FromBase64String(options.Value.EncryptionKey);
        if (encryptionKey.Length != 32)
        {
            throw new InvalidOperationException("PersonalData:EncryptionKey 32 baytlık Base64 değer olmalı.");
        }

        _lookupKey = Convert.FromBase64String(options.Value.LookupKey);
        if (_lookupKey.Length < 32)
        {
            throw new InvalidOperationException("PersonalData:LookupKey en az 32 baytlık Base64 değer olmalı.");
        }

        _aes = new AesGcm(encryptionKey, TagSize);
    }

    public string Protect(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        var plain = Encoding.UTF8.GetBytes(plaintext);
        var buffer = new byte[NonceSize + plain.Length + TagSize];
        var nonce = buffer.AsSpan(0, NonceSize);
        var cipher = buffer.AsSpan(NonceSize, plain.Length);
        var tag = buffer.AsSpan(NonceSize + plain.Length, TagSize);

        RandomNumberGenerator.Fill(nonce);
        _aes.Encrypt(nonce, plain, cipher, tag);

        return Prefix + Convert.ToBase64String(buffer);
    }

    public string Unprotect(string ciphertext)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);
        if (!ciphertext.StartsWith(Prefix, StringComparison.Ordinal))
        {
            throw new CryptographicException("Bilinmeyen şifreleme sürümü.");
        }

        var buffer = Convert.FromBase64String(ciphertext[Prefix.Length..]);
        if (buffer.Length < NonceSize + TagSize)
        {
            throw new CryptographicException("Şifreli veri bozuk.");
        }

        var cipherLength = buffer.Length - NonceSize - TagSize;
        var plain = new byte[cipherLength];
        _aes.Decrypt(
            buffer.AsSpan(0, NonceSize),
            buffer.AsSpan(NonceSize, cipherLength),
            buffer.AsSpan(NonceSize + cipherLength, TagSize),
            plain);

        return Encoding.UTF8.GetString(plain);
    }

    public string ComputeLookupHash(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Convert.ToHexStringLower(HMACSHA256.HashData(_lookupKey, Encoding.UTF8.GetBytes(value)));
    }

    public void Dispose() => _aes.Dispose();
}
