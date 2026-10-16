namespace SahaNet.Application.Identity.Abstractions;

/// <summary>KVKK: kişisel verilerin kolon düzeyinde şifrelenmesi ve aranabilir özeti.</summary>
public interface IPersonalDataProtector
{
    string Protect(string plaintext);

    string Unprotect(string ciphertext);

    /// <summary>Deterministik, anahtarlı özet (HMAC). Şifreli alanda eşitlik araması için.</summary>
    string ComputeLookupHash(string value);
}
