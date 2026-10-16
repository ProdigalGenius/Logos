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

### Veritabanı şeması

`Database:ApplyMigrationsOnStartup=true` (Development) iken: migration varsa uygulanır, yoksa
şema `EnsureCreated` ile oluşturulur. İlk migration'ı üretmek için:

```bash
dotnet tool install --global dotnet-ef
dotnet ef migrations add Faz1_Identity -p src/Infrastructure/SahaNet.Infrastructure -s src/Api/SahaNet.Api -o Persistence/Migrations
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
- [ ] Faz 2 — Konum ve uyarı
- [ ] Faz 3 — Balıkçı çekirdeği
- [ ] Faz 4 — Pazar
- [ ] Faz 5 — Ödeme
- [ ] Faz 6 — Mobil (MAUI)
- [ ] Faz 7 — Yönetim
- [ ] Faz 8 — İkinci niş
