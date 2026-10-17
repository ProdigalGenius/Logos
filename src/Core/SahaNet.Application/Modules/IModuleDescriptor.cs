namespace SahaNet.Application.Modules;

/// <summary>
/// Her dikey modülün (Fishing, Beekeeping, Scrap ...) kendini tanıttığı sözleşme.
/// Niş bazlı build profilleri aktif modül listesini bu anahtarlar üzerinden seçer.
/// </summary>
public interface IModuleDescriptor
{
    /// <summary>Sabit modül anahtarı (örn. "fishing").</summary>
    string Key { get; }

    /// <summary>Kullanıcıya gösterilen ad.</summary>
    string DisplayName { get; }
}
