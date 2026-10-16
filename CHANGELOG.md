# Changelog

Biçim [Keep a Changelog](https://keepachangelog.com/tr-TR/1.1.0/) esas alınır.

## [Unreleased]

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
