namespace SahaNet.Application.Modules;

/// <summary>
/// KVKK md. 11 "verilerimi indir": her modül kendi kişisel verisini bu sözleşmeyle dışa aktarıma ekler.
/// Çekirdek modülleri tanımaz; yalnızca kayıtlı bölümleri toplar.
/// </summary>
public interface IPersonalDataSection
{
    /// <summary>Dışa aktarımdaki bölüm adı (örn. "fishing").</summary>
    string Name { get; }

    Task<object?> ExportAsync(Guid userId, CancellationToken cancellationToken);
}

/// <summary>Modülün kendi şemasını (migration veya ilk oluşturma) hazırlaması için.</summary>
public interface IModuleSchemaInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken);
}
