# Changelog

Biçim [Keep a Changelog](https://keepachangelog.com/tr-TR/1.1.0/) esas alınır.

## [Unreleased]

### Faz 7 — Yönetim
- Rol yönetimi (Moderator/Admin), `Moderator` politikası, askıya alma (giriş/yenileme/ilan engeli, ilanların kaldırılması).
- Şikâyet sistemi ve moderasyon kuyruğu; kararlar: reddet, içeriği kaldır (ilan/mesaj), kullanıcıyı askıya al.
- Kural tabanlı otomatik spam filtresi (engelle / incelemeye al) ilan ve mesajlarda.
- Değiştirilemez denetim kaydı; gösterge paneli metrikleri ve modül metrik sözleşmesi (`IAdminMetricsSection`).
- Balıkçı modülü yönetimi: av yasakları CRUD ve asgari boy güncelleme.
- `/admin` yönetici paneli (bağımlılıksız, katı CSP, XSS'e karşı yalnızca `textContent`).

### Faz 6 — Mobil
- `SahaNet.Mobile.Core`: tipli API istemcisi, 401'de belirteç yenileyen `AuthHandler`, SQLite önbellek + outbox, `SyncService`, offline-first `FishingRepository`, ViewModel'ler (CommunityToolkit.Mvvm), niş profili, Türkçe/İngilizce kaynaklar.
- `SahaNet.Mobile` (.NET MAUI): niş bazlı build profili (`-p:Niche`), kurulum/ana ekran/deniz durumu/av günlüğü ekranları, sesli giriş, güvenli belirteç deposu, bağlantı izleme, Android kritik bildirim kanalı.
- Sunucu: av kaydı ve nokta oluşturmada istemci kimliği (`ClientId`) ile idempotency.
- CI: MAUI Android derleme işi; mobil çekirdek için birim ve mimari testler.

### Faz 5 — Ödeme
- `PaymentOrder` emanet durum makinesi: ödeme bekliyor → emanette → teslim → serbest bırakma; itiraz → yönetici kararı (aktarım/iade); ödenmeyen iptal.
- Komisyon (oran + asgari ücret, kategori bazlı oran, sipariş anında sabitlenir; iadede alınmaz).
- `IPaymentGateway` soyutlaması ve geliştirme için `SandboxPaymentGateway`; üretimde sandbox ile açılış engellendi.
- Ödenmeyen kabul edilmiş teklif iptalinde miktar ilana geri döner (`Listing.CancelAcceptedOffer`).
- Minimum rol modeli (`UserRole`, JWT `role`, `Admin` politikası, `Identity:AdminPhones` ile ilk yönetici).
- `payment-maintenance` Hangfire işi; ödeme olayları için push bildirimleri; komisyon özeti.

### Faz 4 — Pazar
- Çekirdek ilan aggregate'i: sabit fiyat (kısmi miktar ön sipariş), teklif toplama, süreli ihale (asgari artış, son dakika uzatma, gizli taban fiyat).
- `xmin` satır sürümüyle eşzamanlı teklif koruması; dakikalık `listing-close` Hangfire işi.
- `IListingCategoryProvider` ile modül bazında genişleyen kategori/alan şeması ve doğrulayıcı.
- Balıkçı modülü: `fresh-fish` ve `fishing-equipment` kategorileri, hal fiyatları (medyan).
- Mesajlaşma: sohbet, mesaj, okunmamış sayısı, SignalR hub'ı, hız sınırları, push bildirimleri.
- WhatsApp paylaşım linki; pazar verisi KVKK dışa aktarımına eklendi.
- Düzeltme: av noktası paylaşımında owned kayda üretilen anahtar (yanlış UPDATE → 409).

### Faz 3 — Balıkçı çekirdeği
- Modül kendi `FishingDbContext`'i ve `fishing` şemasıyla; çekirdeğe `IPersonalDataSection` ve `IModuleSchemaInitializer` sözleşmeleri eklendi.
- Liman bazlı deniz durumu: Beaufort, Türkçe rüzgâr adları, yerel gün özeti, Go/Caution/NoGo kararı, aktif uyarılar.
- Av noktası defteri: şifreli koordinat, varsayılan gizli, telefonla paylaşım (konum rızası şart), ücretsiz sürüm sınırı.
- `LocationSharingWithdrawn` event'i: rıza geri çekilince paylaşımlar otomatik kaldırılır.
- Av günlüğü: asgari boy ve tür yasağı uyarıları, sezon istatistiği.
- Tür kataloğu (24 tür) ve av yasakları takvimi (taslak seed), 22 liman.
- Hesap kalıcı silinince modül verisi silinir; dışa aktarıma `modules.fishing` bölümü eklendi.

### Faz 2 — Konum ve uyarı
- PostGIS: `Region` (geography nokta + yarıçap, GiST indeks), 13 kıyı deniz alanı seed'i, `ST_DWithin` ile yakın bölge sorgusu.
- Bölge aboneliği (konu bayrakları, en fazla 10 bölge) ve FCM cihaz kaydı (token el değiştirirse yeni sahibe geçer).
- Open-Meteo rüzgâr + dalga istemcisi, Redis önbellek, `AddStandardResilienceHandler`.
- Beaufort tabanlı uyarı değerlendirici, tekrar üretimi engelleyen dedup anahtarı, Türkçe uyarı metni.
- Uyarı dağıtımı: push → kritikte SMS yedeği, geçersiz token temizliği, idempotent teslim kaydı.
- FCM (kritik kanal/yüksek öncelik) ve Netgsm SMS sağlayıcıları; geliştirme için loglayan sağlayıcılar.
- Hangfire (PostgreSQL): hava kontrolü, 30 günlük hesap silme ve KVKK saklama süresi işleri.
- Verilerimi indir çıktısına abonelikler ve cihazlar eklendi.
- CI: OpenTelemetry güvenlik açığı nedeniyle 1.20.0'a yükseltildi; Newtonsoft.Json 13.0.4'e sabitlendi.

### Faz 1 — Kimlik
- Telefon + SMS OTP ile giriş; numara başına bekleme/saatlik limit, hatalı denemede kilit, IP bazlı rate limit.
- JWT erişim belirteci + döndürülen (rotation) yenileme belirteci, tekrar kullanımda aile iptali.
- `User`, `PhoneNumber`, `OtpChallenge`, `RefreshToken`, `ConsentDocument`, `UserConsent` domain modeli.
- Onboarding: niş (modül kataloğu) ve il seçimi; zorunlu KVKK rızaları olmadan tamamlanamaz.
- Versiyonlu, yalnızca eklenen rıza kayıtları; taslak aydınlatma/açık rıza/konum/ticari ileti metinleri.
- KVKK: verilerimi indir, hesap silme talebi/iptali, 30 gün sonra kalıcı silme komutu.
- Telefon numarası AES-256-GCM şifreli kolonda, HMAC arama özetiyle; loglarda otomatik maskeleme.
- `Result`/`ErrorType` → ProblemDetails eşlemesi, doğrulama hataları için 400.
- Birim testleri (domain, handler, kripto) ve Testcontainers ile uçtan uca kimlik akışı testleri.

### Faz 0 — İskelet
- Clean Architecture çözüm yapısı (Domain, Application, Infrastructure, Modules.Fishing, Api).
- Merkezi paket yönetimi (`Directory.Packages.props`); nullable açık, uyarılar hata.
- Domain temel tipleri: `Entity`, `AggregateRoot`, `ValueObject`, `Result`, `DomainEvent`.
- MediatR + FluentValidation `ValidationBehavior` pipeline'ı; domain event yayını için
  `DomainEventNotification<TEvent>`.
- EF Core 9 + Npgsql + NetTopologySuite (PostGIS) `SahaNetDbContext`; event'ler kayıttan sonra yayınlanır.
- Options pattern ile doğrulanan `Database` ve `Redis` ayarları.
- `/health/live` ve `/health/ready` (PostgreSQL, Redis) uçları; Serilog, OpenTelemetry, Swagger.
- Docker Compose (PostGIS 16, Redis 7, isteğe bağlı API), API Dockerfile.
- GitHub Actions CI: build, birim/mimari testler, Testcontainers entegrasyon testleri, kapsam raporu, Docker imajı.
- NetArchTest mimari testleri, birim testleri ve entegrasyon testleri.
