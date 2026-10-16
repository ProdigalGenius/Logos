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
| `/swagger` | OpenAPI arayüzü (Development) |

Konfigürasyon Options pattern ile (`Database`, `Redis` bölümleri) okunur ve açılışta doğrulanır.
OTLP izleme için `OTEL_EXPORTER_OTLP_ENDPOINT` ortam değişkeni verilmesi yeterli.

## Test

```bash
dotnet test SahaNet.sln --collect:"XPlat Code Coverage"
```

Entegrasyon testleri Docker gerektirir (`postgis/postgis:16-3.4`, `redis:7-alpine`).

## Fazlar

- [x] Faz 0 — İskelet
- [ ] Faz 1 — Kimlik (OTP, JWT, profil, KVKK rıza)
- [ ] Faz 2 — Konum ve uyarı
- [ ] Faz 3 — Balıkçı çekirdeği
- [ ] Faz 4 — Pazar
- [ ] Faz 5 — Ödeme
- [ ] Faz 6 — Mobil (MAUI)
- [ ] Faz 7 — Yönetim
- [ ] Faz 8 — İkinci niş
