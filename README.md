# SahaNet

Dijitalleşmemiş meslek ve topluluklar (balıkçı, arıcı, hurdacı, nakliyeci, çoban …) için
ortak çekirdek üzerine kurulu, dikey modüllü platform. Arka uç tek; her niş ayrı marka
(white-label) olarak yayınlanır. İlk niş: **BalıkçıNet** (tekne balıkçıları).

## Mimari

```
src/
  Core/SahaNet.Domain             Entity, AggregateRoot, ValueObject, Result, domain event'leri (bağımlılıksız)
  Core/SahaNet.Application        Use-case'ler, MediatR + FluentValidation pipeline, modül sözleşmesi
  Infrastructure/SahaNet.Infrastructure  EF Core 9 + PostgreSQL/PostGIS, Redis, health check'ler
  Modules/SahaNet.Modules.Fishing Balıkçı modülü (yalnızca Core'a bağlı)
  Api/SahaNet.Api                 Minimal API, Serilog, OpenTelemetry, Swagger, health uçları
tests/
  SahaNet.UnitTests               xUnit + FluentAssertions
  SahaNet.IntegrationTests        WebApplicationFactory + Testcontainers (gerçek PostGIS & Redis)
  SahaNet.ArchitectureTests       NetArchTest ile katman kuralları
```

**Kurallar** (mimari testlerle zorlanır):

- Domain hiçbir katmana, MediatR'a veya EF Core'a bağlı değildir.
- Application; Infrastructure, Api ve modüllere bağlı değildir.
- Infrastructure; Api ve modüllere bağlı değildir.
- Modüller yalnızca Core'a bağlıdır, **birbirini referans almaz**. Modüller arası iletişim
  `DomainEventNotification<TEvent>` üzerinden domain event'leri ile yapılır.

Yeni modül eklemek: `src/Modules/SahaNet.Modules.<Ad>` oluştur, `IModuleDescriptor` uygula,
`Add<Ad>Module()` / `Map<Ad>Module()` uzantılarını yaz, Api'de çağır ve
`tests/SahaNet.ArchitectureTests/LayerTests.cs` içindeki `ModuleAssemblies` listesine ekle.

## Gereksinimler

- .NET SDK 9.0.100+
- Docker (Compose ve Testcontainers için)

## Çalıştırma

```bash
docker compose up -d                 # PostgreSQL 16 + PostGIS, Redis
dotnet run --project src/Api/SahaNet.Api
# veya her şeyi konteynerde:
docker compose --profile api up --build
```

| Uç nokta | Açıklama |
|---|---|
| `GET /health/live` | Süreç ayakta mı |
| `GET /health/ready` | PostgreSQL ve Redis erişilebilir mi |
| `GET /api/modules` | Yüklü dikey modüller |
| `GET /api/fishing/info` | Balıkçı modülü bilgisi |
| `GET /api/regions` | İl listesi (bölge seçimi) |
| `GET /api/consents/documents` | Yürürlükteki aydınlatma / rıza metinleri |
| `POST /api/auth/otp/request` | Telefona SMS kodu gönder |
| `POST /api/auth/otp/verify` | Kodu doğrula, oturum aç (yeni kullanıcıyı oluşturur) |
| `POST /api/auth/token/refresh` | Yenileme belirtecini döndür |
| `POST /api/auth/logout` | Oturumu kapat |
| `GET /api/me` | Profilim |
| `POST /api/me/consents` · `GET /api/me/consents` | Rıza kaydet / geçmişim |
| `POST /api/me/onboarding` | Niş + il seçimi (zorunlu rızalar şart) |
| `PUT /api/me/profile` | Ad / il güncelle |
| `GET /api/me/export` | KVKK: verilerimi indir |
| `DELETE /api/me` · `POST /api/me/deletion/cancel` | KVKK: hesabımı sil (30 gün) / iptal |
| `GET /api/geo/regions` | Deniz bölgeleri |
| `GET /api/geo/regions/nearby?lat=&lon=` | Konumu kapsayan bölgeler (PostGIS, konum saklanmaz) |
| `GET /api/geo/regions/{id}/forecast?days=` | Saatlik rüzgâr/dalga tahmini + aktif uyarılar |
| `GET /api/alerts/active?regionId=` | Aktif uyarılar |
| `GET/PUT/DELETE /api/me/subscriptions/{regionId}` | Bölge aboneliği (konular: Weather, Market, Community) |
| `POST /api/me/devices` · `POST /api/me/devices/unregister` | Push (FCM) cihaz kaydı |
| `GET /api/fishing/ports?lat=&lon=` | Limanlar (konuma göre sıralı) |
| `GET /api/fishing/ports/{code}/sea-status?days=` | Deniz durumu: günlük özet, Beaufort, rüzgâr adı, çıkış kararı |
| `GET /api/fishing/species` · `GET /api/fishing/bans` | Tür kataloğu (asgari boy) ve av yasakları takvimi |
| `GET/POST/PUT/DELETE /api/fishing/spots` | Av noktası defteri (şifreli, varsayılan gizli) |
| `POST/DELETE /api/fishing/spots/{id}/shares` | Telefonla paylaşım (konum rızası gerekir) |
| `GET/POST/DELETE /api/fishing/catches` · `GET .../stats` | Av günlüğü ve sezon istatistiği |
| `GET /api/market/categories?niche=` | Modüllerin ilan kategorileri ve alan şemaları |
| `GET /api/market/listings?niche=&category=&region=&type=&q=` | İlan arama |
| `POST /api/market/listings` · `POST .../{id}/cancel` | İlan ver (sabit fiyat / teklif / ihale) · kapat |
| `POST /api/market/listings/{id}/offers` | Teklif, ön sipariş veya ihale teklifi |
| `POST /api/market/offers/{id}/accept\|reject\|withdraw` | Teklif kararları |
| `GET /api/me/listings` · `GET /api/me/offers` | İlanlarım · tekliflerim |
| `GET/POST /api/conversations` · `GET/POST .../{id}/messages` | Sohbetler ve mesajlar |
| `/hubs/chat` (SignalR) | Canlı mesaj (`messageReceived`), `SendMessage(conversationId, text)` |
| `GET/POST /api/fishing/prices` | Hal fiyatları (medyan) |
| `POST /api/payments/offers/{offerId}/checkout` | Kabul edilen teklif için emanetli ödeme başlat |
| `POST /api/payments/callback` | Ödeme sağlayıcısı geri çağrısı (durum sağlayıcıdan doğrulanır) |
| `POST /api/payments/orders/{id}/delivered\|confirm\|dispute` | Teslim ettim · teslim aldım · itiraz |
| `GET /api/me/payments` | Ödemelerim (alıcı/satıcı) |
| `GET /api/admin/payments/disputes` · `POST .../orders/{id}/resolve` · `GET .../commission` | Yönetici: itirazlar, karar, komisyon özeti |
| `POST /api/payments/sandbox/{token}/pay` | YALNIZCA GELİŞTİRME: ödeme simülasyonu |
| `/hangfire` | Arka plan işleri paneli (Development, yalnızca localhost) |
| `/swagger` | OpenAPI arayüzü (Development) |

Konfigürasyon Options pattern ile (`Database`, `Redis`, `Identity`, `Jwt`, `PersonalData`, `Sms`,
`RateLimiting` bölümleri) okunur ve açılışta doğrulanır.

### Kimlik ve KVKK (Faz 1)

Kurulum 3 adım: **telefon → SMS kodu → rızalar + niş ve il seçimi**.

- **OTP:** 6 haneli kod, 3 dk geçerli; yalnızca HMAC özeti saklanır. Numara başına 60 sn bekleme,
  saatte en fazla 5 kod, 5 hatalı denemede kilit; ayrıca IP bazlı rate limit.
- **Oturum:** 15 dk JWT + 30 gün opak yenileme belirteci (veritabanında yalnızca özeti).
  Her yenilemede belirteç döner; eski belirteç tekrar kullanılırsa tüm aile iptal edilir.
- **Şifreli kolon:** Telefon AES-256-GCM ile şifrelenir (`phone_encrypted`), arama HMAC-SHA256
  özetiyle (`phone_hash`) yapılır. Anahtarlar `PersonalData:EncryptionKey` / `LookupKey`
  (Base64, 32 bayt) üretimde gizli kasadan verilmeli.
- **Rıza:** Metinler versiyonlu; kararlar yalnızca eklenir (geri çekme de kayıttır). Yeni sürüm
  yayınlanınca yeniden onay gerekir. Konum paylaşımı rızası varsayılan olarak **verilmemiş**.
- **Loglar:** Telefon numaraları Serilog enricher ile otomatik maskelenir.
- **Silme:** `DELETE /api/me` hesabı işaretler ve tüm oturumları kapatır;
  `PurgeDeletedAccountsCommand` 30 gün sonra kalıcı siler (Faz 2'de Hangfire ile zamanlanacak).

> Seed edilen KVKK metinleri **taslaktır**; yayından önce hukuk onayı gerekir.

### Konum ve uyarılar (Faz 2)

- **Bölgeler:** 13 kıyı deniz alanı seed edilir (merkez + yarıçap, `geography(Point,4326)` + GiST).
  "Yakınımdaki bölgeler" PostGIS `ST_DWithin` ile bulunur; kullanıcının konumu saklanmaz.
- **Hava/deniz verisi:** Open-Meteo (rüzgâr + dalga), Redis'te 30 dk önbellek, standart dayanıklılık
  (retry/timeout/circuit breaker). Ücretsiz uç nokta ticari değildir; üretimde `Weather:ApiKey` gerekir.
- **Eşikler (Beaufort):** Uyarı ≥ 39 km/s rüzgâr, ≥ 50 km/s hamle veya ≥ 2 m dalga;
  kritik (fırtına) ≥ 62 km/s, ≥ 75 km/s hamle veya ≥ 3,5 m dalga. Önümüzdeki 24 saate bakılır.
  Aynı bölge/seviye/gün için ikinci uyarı üretilmez.
- **Dağıtım:** Uyarı seviyesi yalnızca `Weather` konusuna abone olanlara; kritik uyarı bölgenin tüm
  abonelerine gider. Kritikte Android'de yüksek öncelik + `critical_alerts` kanalı (mobil uygulama bu
  kanalı "Rahatsız Etmeyin"i delecek şekilde oluşturur), iOS'ta `time-sensitive`. Push ulaşmazsa SMS.
  Teslimler kaydedilir; iş tekrar çalışsa da aynı kişiye ikinci kez gitmez.
- **Sağlayıcılar:** `Push:Provider` = `Logging` | `Fcm` (`Push:FcmCredentialsPath`),
  `Sms:Provider` = `Logging` | `Netgsm` (`Sms:Netgsm:Username/Password/Header`).
- **Periyodik işler (Hangfire, PostgreSQL deposu, Europe/Istanbul):**

  | İş | Varsayılan | Görev |
  |---|---|---|
  | `weather-alert-check` | `*/30 * * * *` | Abonesi olan bölgeleri kontrol et, uyarı yayınla |
  | `account-purge` | `0 3 * * *` | 30 günü dolan silme taleplerini kalıcı sil |
  | `data-retention` | `30 3 * * *` | Saklama süresi dolan kayıtları sil |

- **Saklama süreleri (`Retention`):** OTP 1 gün, pasif yenileme belirteci 30 gün, teslim kaydı 90 gün,
  süresi geçmiş uyarı 365 gün, 90 gün görülmeyen cihaz.

`BackgroundJobs:Enabled=false` iken Hangfire başlatılmaz ve uyarı dağıtımı satır içi çalışır (testler).

### Balıkçı modülü (Faz 3)

Modül yalnızca Core'a bağlıdır; kendi `FishingDbContext`'i ve `fishing` şeması vardır (çekirdek
tablolarına FK yok). Çekirdekle sözleşmeler üzerinden konuşur:

- `IPersonalDataSection` → "verilerimi indir" çıktısına `modules.fishing` bölümü,
- `DomainEventNotification<UserPurged>` → hesap kalıcı silinince noktalar, paylaşımlar ve av günlüğü silinir,
- `DomainEventNotification<LocationSharingWithdrawn>` → konum rızası geri çekilince tüm paylaşımlar kalkar,
- `IModuleSchemaInitializer` → şema, çekirdek başlatıcısından sonra hazırlanır.

Özellikler:

- **Deniz durumu:** 22 balıkçı limanı; liman noktası için tahmin, Beaufort sayısı, Türkçe rüzgâr adları
  (Yıldız, Poyraz, Gündoğusu, Keşişleme, Kıble, Lodos, Günbatısı, Karayel), yerel güne göre özet ve
  🟢/🟡/🔴 karar (`Go` / `Caution` / `NoGo`). Limanı kapsayan bölgede kritik uyarı varsa karar `NoGo`.
- **Av noktası defteri:** Koordinatlar AES-GCM ile şifreli (`location_encrypted`), varsayılan gizli.
  Paylaşım telefon numarasıyla, yalnızca konum paylaşımı rızası varken. Notlar yalnız sahibine görünür.
  Ücretsiz sürümde 20 nokta (`Fishing:FreeSpotLimit`).
- **Av günlüğü:** Tür, kg, adet, boy, nokta/liman. Kayıt engellenmez; asgari boy altı ve türe özel yasak
  döneminde uyarı döner. Sezon istatistiği 1 Eylül – 31 Ağustos (tür, ay, avda geçen gün).
- **Yasak takvimi:** Yıllık tekrarlayan dönemler (yıl sonunu aşanlar dahil), tür/bölge/av aracı filtresi.

> Tür asgari boyları ve yasak dönemleri **taslaktır**; yürürlükteki Ticari Amaçlı Su Ürünleri
> Avcılığı Tebliği ile doğrulanmalıdır (Faz 7'de yönetici panelinden güncellenecek).

### Pazar ve mesajlaşma (Faz 4)

- **İlan türleri:** `FixedPrice` (alıcı miktar belirterek ön sipariş/rezervasyon ister; kabul edilen
  miktar stoktan düşer), `OfferBased` (satıcı teklif seçer, kalanlar otomatik reddedilir), `Auction`
  (en yüksek teklif + asgari artış; son 2 dakikadaki teklif süreyi uzatır; süre sonunda gizli taban
  fiyatı geçen teklif kazanır, `listing-close` işi dakikalık çalışır). İhale teklifi geri çekilemez.
- **Eşzamanlılık:** İlan satırı her teklifte güncellenir ve PostgreSQL `xmin` satır sürümüyle korunur;
  aynı anda gelen iki teklif çakışırsa biri 409 alır.
- **Kategori şeması:** Modüller `IListingCategoryProvider` ile kategori ve alan tanımı verir; çekirdek
  alanları doğrular ve normalize eder (sayılarda virgül, tarihler UTC). Balıkçı modülü: `fresh-fish`
  (tür, teslim limanı, teslim zamanı, av aracı) ve `fishing-equipment` (ağ/motor/tekne, durum, marka …).
- **Gizlilik:** Teklif ayrıntılarını yalnızca satıcı ve teklif sahibi görür; ihalede herkes yalnızca en
  yüksek teklif tutarını görür.
- **Bildirim:** Yeni teklif, kabul/ret, teklifin geçilmesi ve yeni mesaj için push.
- **Mesajlaşma:** İlan başına tek birebir sohbet; okunmamış sayısı; metin + sesli/foto (https medya
  adresi). SignalR (`/hubs/chat`, JWT `access_token` sorgu parametresiyle), REST ile de gönderilebilir.
  Kullanıcı başına dakikada 20 mesaj + token bucket hız sınırı.
- **WhatsApp:** Her ilan yanıtında `shareUrl` (`https://wa.me/?text=…`).
- **Hal fiyatları:** Kullanıcılar tür/il/gün için TL/kg bildirir (kişi başı tek kayıt); günlük özet
  medyan, en düşük, en yüksek ve bildirim sayısıyla döner.
- **KVKK:** Dışa aktarımda `modules.market` (ilanlar, teklifler, gönderilen mesajlar); hesap silinince
  kayıtlar FK ile silinir.

### Ödeme ve emanet (Faz 5)

```
AwaitingPayment ──öde──▶ Held ──satıcı: teslim ettim──▶ Delivered ──alıcı onayı / 48 sa──▶ Released
      │                    │                              │
  30 dk ödenmedi       alıcı onayı ───────────────────────┼──────────────────────────▶ Released
      ▼                    └──────── itiraz ──────────────┴──▶ Disputed ──yönetici──▶ Released | Refunded
  Cancelled (miktar ilana geri döner)
```

- **Komisyon:** `max(tutar × oran, asgari ücret)`, tutarı aşamaz; oran sipariş anında sabitlenir
  (`Payments:DefaultCommissionRate`, kategori bazlı `CategoryCommissionRates`). İadede komisyon alınmaz.
  Komisyonu ve satıcıya geçecek tutarı yalnızca satıcı ve yönetici görür.
- **Güvenlik:** Geri çağrıdaki veriye güvenilmez; ödeme sağlayıcıdan doğrulanır, tutar karşılaştırılır,
  geri çağrı idempotenttir. Sağlayıcı aktarımı reddederse durum kaydedilmez. Satır sürümü (`xmin`).
- **Bakım işi** (`payment-maintenance`, 5 dk): ödenmeyenleri iptal eder (rezervasyon geri açılır),
  itiraz süresi dolanları satıcıya aktarır.
- **Yönetici:** `Identity:AdminPhones` listesindeki numaralar girişte `Admin` rolü alır (JWT `role`);
  itiraz kararı ve komisyon özeti yalnızca yöneticiye açıktır. Tam rol yönetimi Faz 7'de.
- **KVKK/VUK:** Ödeme kayıtları kullanıcıya FK ile bağlı değildir; hesap silinse de mali kayıt olarak
  (kanuni saklama yükümlülüğü) yalnızca anlamsız kimliklerle saklanır.
- **Sağlayıcı:** Şimdilik yalnızca `SandboxPaymentGateway` vardır. iyzico/PayTR pazaryeri adaptörü
  (`IPaymentGateway`: checkout, doğrulama, alt üye işyerine aktarım onayı, iade) üye işyeri bilgileri ve
  sandbox erişimiyle eklenmelidir. **Uygulama Development/Testing dışında başlatılırsa açılışta durur.**

### Veritabanı şeması

`Database:ApplyMigrationsOnStartup=true` (Development) iken: migration varsa uygulanır, yoksa
şema `EnsureCreated` ile oluşturulur. İlk migration'ı üretmek için:

```bash
dotnet tool install --global dotnet-ef
dotnet ef migrations add Initial -p src/Infrastructure/SahaNet.Infrastructure -s src/Api/SahaNet.Api -c SahaNetDbContext -o Persistence/Migrations
dotnet ef migrations add Initial -p src/Modules/SahaNet.Modules.Fishing -s src/Api/SahaNet.Api -c FishingDbContext -o Persistence/Migrations
```

Migration eklendikten sonra `EnsureCreated` ile oluşturulmuş yerel veritabanını silin
(`docker compose down -v`).
OTLP izleme için `OTEL_EXPORTER_OTLP_ENDPOINT` ortam değişkeni verilmesi yeterli.

## Test

```bash
dotnet test SahaNet.sln --collect:"XPlat Code Coverage"
```

Entegrasyon testleri Docker gerektirir (`postgis/postgis:16-3.4`, `redis:7-alpine`).

## Fazlar

- [x] Faz 0 — İskelet
- [x] Faz 1 — Kimlik (OTP, JWT, profil, KVKK rıza)
- [x] Faz 2 — Konum ve uyarı
- [x] Faz 3 — Balıkçı çekirdeği
- [x] Faz 4 — Pazar
- [x] Faz 5 — Ödeme (sandbox sağlayıcı)
- [ ] Faz 6 — Mobil (MAUI)
- [ ] Faz 7 — Yönetim
- [ ] Faz 8 — İkinci niş
