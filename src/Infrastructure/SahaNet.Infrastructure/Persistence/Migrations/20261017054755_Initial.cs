using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SahaNet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "alerts");

            migrationBuilder.EnsureSchema(
                name: "moderation");

            migrationBuilder.EnsureSchema(
                name: "identity");

            migrationBuilder.EnsureSchema(
                name: "market");

            migrationBuilder.EnsureSchema(
                name: "payments");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.CreateTable(
                name: "audit_log",
                schema: "moderation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TargetType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TargetId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Details = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_log", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "consent_documents",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Version = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consent_documents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "conversations",
                schema: "market",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ListingId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastMessageAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conversations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "orders",
                schema: "payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ListingId = table.Column<Guid>(type: "uuid", nullable: false),
                    OfferId = table.Column<Guid>(type: "uuid", nullable: false),
                    BuyerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CommissionRate = table.Column<decimal>(type: "numeric(6,4)", precision: 6, scale: 4, nullable: false),
                    Commission = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    ProviderPaymentId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PayBy = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PaidAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeliveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AutoReleaseAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DisputeReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DisputedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Decision = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    DecisionNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DecidedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_orders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "otp_challenges",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PhoneHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FailedAttempts = table.Column<int>(type: "integer", nullable: false),
                    ConsumedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_otp_challenges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "regions",
                schema: "alerts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    center = table.Column<Point>(type: "geography (point, 4326)", nullable: false),
                    radius_km = table.Column<double>(type: "double precision", nullable: false),
                    province_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_regions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "reports",
                schema: "moderation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReporterId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetOwnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SpamScore = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResolvedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    ResolutionNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    phone_encrypted = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    phone_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    NicheKey = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    RegionCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastLoginAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletionRequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SuspensionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "alerts",
                schema: "alerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RegionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Severity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ValidFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ValidTo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DedupKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_alerts_regions_RegionId",
                        column: x => x.RegionId,
                        principalSchema: "alerts",
                        principalTable: "regions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "conversation_participants",
                schema: "market",
                columns: table => new
                {
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    JoinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conversation_participants", x => new { x.ConversationId, x.UserId });
                    table.ForeignKey(
                        name: "FK_conversation_participants_conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalSchema: "market",
                        principalTable: "conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_conversation_participants_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "devices",
                schema: "alerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Platform = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_devices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_devices_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "listings",
                schema: "market",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: false),
                    NicheKey = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CategoryKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    RemainingQuantity = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    Unit = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RegionCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    Attributes = table.Column<string>(type: "jsonb", nullable: false),
                    MinBidIncrement = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    ReservePrice = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastActivityAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModerationNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_listings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_listings_users_SellerId",
                        column: x => x.SellerId,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "messages",
                schema: "market",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    MediaUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsRedacted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_messages_conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalSchema: "market",
                        principalTable: "conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_messages_users_SenderId",
                        column: x => x.SenderId,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FamilyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReplacedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "region_subscriptions",
                schema: "alerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RegionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Topics = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_region_subscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_region_subscriptions_regions_RegionId",
                        column: x => x.RegionId,
                        principalSchema: "alerts",
                        principalTable: "regions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_region_subscriptions_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_consents",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Version = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Granted = table.Column<bool>(type: "boolean", nullable: false),
                    Source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_consents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_consents_consent_documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "identity",
                        principalTable: "consent_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_consents_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "alert_deliveries",
                schema: "alerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AlertId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_deliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_alert_deliveries_alerts_AlertId",
                        column: x => x.AlertId,
                        principalSchema: "alerts",
                        principalTable: "alerts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_alert_deliveries_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "offers",
                schema: "market",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ListingId = table.Column<Guid>(type: "uuid", nullable: false),
                    BuyerId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    Message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AcceptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_offers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_offers_listings_ListingId",
                        column: x => x.ListingId,
                        principalSchema: "market",
                        principalTable: "listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_offers_users_BuyerId",
                        column: x => x.BuyerId,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "identity",
                table: "consent_documents",
                columns: new[] { "Id", "Content", "IsActive", "IsRequired", "PublishedAt", "Title", "Type", "Version" },
                values: new object[,]
                {
                    { new Guid("0b1a7c52-4a8e-4b0e-9a51-5e0f6a1c0001"), "TASLAK — 6698 sayılı KVKK kapsamında veri sorumlusu, işlenen veriler (telefon, profil, konum), amaçlar, aktarım ve haklarınız hakkında bilgilendirme.", true, true, new DateTimeOffset(new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Aydınlatma Metni", "PrivacyNotice", "1.0" },
                    { new Guid("0b1a7c52-4a8e-4b0e-9a51-5e0f6a1c0002"), "TASLAK — Hizmetin sunulması için kişisel verilerimin işlenmesine açık rıza veriyorum.", true, true, new DateTimeOffset(new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Açık Rıza Metni", "ExplicitConsent", "1.0" },
                    { new Guid("0b1a7c52-4a8e-4b0e-9a51-5e0f6a1c0003"), "TASLAK — Konum verilerim varsayılan olarak gizlidir. Bu rızayla seçtiğim konumların paylaşılmasına izin veririm.", true, false, new DateTimeOffset(new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Konum Paylaşımı Rızası", "LocationSharing", "1.0" },
                    { new Guid("0b1a7c52-4a8e-4b0e-9a51-5e0f6a1c0004"), "TASLAK — Kampanya ve duyurular için SMS/bildirim almayı kabul ediyorum.", true, false, new DateTimeOffset(new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Ticari İleti İzni", "Marketing", "1.0" }
                });

            migrationBuilder.InsertData(
                schema: "alerts",
                table: "regions",
                columns: new[] { "id", "center", "code", "is_active", "kind", "name", "province_code", "radius_km" },
                values: new object[,]
                {
                    { new Guid("5e0c1a00-0000-4000-8000-000000000001"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (29.06 41.12)"), "istanbul-bogazi", true, "SeaArea", "İstanbul Boğazı", "34", 25.0 },
                    { new Guid("5e0c1a00-0000-4000-8000-000000000002"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (29 40.75)"), "marmara-dogu", true, "SeaArea", "Doğu Marmara", "34", 45.0 },
                    { new Guid("5e0c1a00-0000-4000-8000-000000000003"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (27.8 40.65)"), "marmara-bati", true, "SeaArea", "Batı Marmara", "59", 55.0 },
                    { new Guid("5e0c1a00-0000-4000-8000-000000000004"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (26.4 40.2)"), "canakkale-bogazi", true, "SeaArea", "Çanakkale Boğazı", "17", 35.0 },
                    { new Guid("5e0c1a00-0000-4000-8000-000000000005"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (30.5 41.6)"), "karadeniz-bati", true, "SeaArea", "Batı Karadeniz", null, 120.0 },
                    { new Guid("5e0c1a00-0000-4000-8000-000000000006"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (34.5 42)"), "karadeniz-orta", true, "SeaArea", "Orta Karadeniz", null, 150.0 },
                    { new Guid("5e0c1a00-0000-4000-8000-000000000007"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (39.5 41.3)"), "karadeniz-dogu", true, "SeaArea", "Doğu Karadeniz", null, 150.0 },
                    { new Guid("5e0c1a00-0000-4000-8000-000000000008"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (26 39.6)"), "ege-kuzey", true, "SeaArea", "Kuzey Ege", null, 100.0 },
                    { new Guid("5e0c1a00-0000-4000-8000-000000000009"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (26.4 38.5)"), "ege-orta", true, "SeaArea", "Orta Ege", null, 90.0 },
                    { new Guid("5e0c1a00-0000-4000-8000-000000000010"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (27 37.2)"), "ege-guney", true, "SeaArea", "Güney Ege", null, 90.0 },
                    { new Guid("5e0c1a00-0000-4000-8000-000000000011"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (29.5 36.3)"), "akdeniz-bati", true, "SeaArea", "Batı Akdeniz", null, 120.0 },
                    { new Guid("5e0c1a00-0000-4000-8000-000000000012"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (32.5 36)"), "akdeniz-orta", true, "SeaArea", "Orta Akdeniz", null, 140.0 },
                    { new Guid("5e0c1a00-0000-4000-8000-000000000013"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (35.2 36.5)"), "akdeniz-dogu", true, "SeaArea", "Doğu Akdeniz", null, 120.0 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_alert_deliveries_AlertId_UserId",
                schema: "alerts",
                table: "alert_deliveries",
                columns: new[] { "AlertId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_alert_deliveries_CreatedAt",
                schema: "alerts",
                table: "alert_deliveries",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_alert_deliveries_UserId",
                schema: "alerts",
                table: "alert_deliveries",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_alerts_DedupKey",
                schema: "alerts",
                table: "alerts",
                column: "DedupKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_alerts_RegionId_ValidTo",
                schema: "alerts",
                table: "alerts",
                columns: new[] { "RegionId", "ValidTo" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_At",
                schema: "moderation",
                table: "audit_log",
                column: "At");

            migrationBuilder.CreateIndex(
                name: "IX_consent_documents_Type_Version",
                schema: "identity",
                table: "consent_documents",
                columns: new[] { "Type", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_conversation_participants_UserId",
                schema: "market",
                table: "conversation_participants",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_conversations_ListingId",
                schema: "market",
                table: "conversations",
                column: "ListingId");

            migrationBuilder.CreateIndex(
                name: "IX_devices_TokenHash",
                schema: "alerts",
                table: "devices",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_devices_UserId",
                schema: "alerts",
                table: "devices",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_listings_SellerId",
                schema: "market",
                table: "listings",
                column: "SellerId");

            migrationBuilder.CreateIndex(
                name: "IX_listings_Status_ExpiresAt",
                schema: "market",
                table: "listings",
                columns: new[] { "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_listings_Status_NicheKey_CategoryKey_CreatedAt",
                schema: "market",
                table: "listings",
                columns: new[] { "Status", "NicheKey", "CategoryKey", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_messages_ConversationId_SentAt",
                schema: "market",
                table: "messages",
                columns: new[] { "ConversationId", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_messages_SenderId_SentAt",
                schema: "market",
                table: "messages",
                columns: new[] { "SenderId", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_offers_BuyerId",
                schema: "market",
                table: "offers",
                column: "BuyerId");

            migrationBuilder.CreateIndex(
                name: "IX_offers_ListingId",
                schema: "market",
                table: "offers",
                column: "ListingId");

            migrationBuilder.CreateIndex(
                name: "IX_orders_BuyerId",
                schema: "payments",
                table: "orders",
                column: "BuyerId");

            migrationBuilder.CreateIndex(
                name: "IX_orders_OfferId",
                schema: "payments",
                table: "orders",
                column: "OfferId");

            migrationBuilder.CreateIndex(
                name: "IX_orders_ProviderPaymentId",
                schema: "payments",
                table: "orders",
                column: "ProviderPaymentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_orders_SellerId",
                schema: "payments",
                table: "orders",
                column: "SellerId");

            migrationBuilder.CreateIndex(
                name: "IX_orders_Status_AutoReleaseAt",
                schema: "payments",
                table: "orders",
                columns: new[] { "Status", "AutoReleaseAt" });

            migrationBuilder.CreateIndex(
                name: "IX_orders_Status_PayBy",
                schema: "payments",
                table: "orders",
                columns: new[] { "Status", "PayBy" });

            migrationBuilder.CreateIndex(
                name: "IX_otp_challenges_PhoneHash_CreatedAt",
                schema: "identity",
                table: "otp_challenges",
                columns: new[] { "PhoneHash", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_FamilyId",
                schema: "identity",
                table: "refresh_tokens",
                column: "FamilyId");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_TokenHash",
                schema: "identity",
                table: "refresh_tokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_UserId",
                schema: "identity",
                table: "refresh_tokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_region_subscriptions_RegionId",
                schema: "alerts",
                table: "region_subscriptions",
                column: "RegionId");

            migrationBuilder.CreateIndex(
                name: "IX_region_subscriptions_UserId_RegionId",
                schema: "alerts",
                table: "region_subscriptions",
                columns: new[] { "UserId", "RegionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_regions_center",
                schema: "alerts",
                table: "regions",
                column: "center")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_regions_code",
                schema: "alerts",
                table: "regions",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reports_ReporterId_CreatedAt",
                schema: "moderation",
                table: "reports",
                columns: new[] { "ReporterId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_reports_Status_CreatedAt",
                schema: "moderation",
                table: "reports",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_reports_TargetType_TargetId_Status",
                schema: "moderation",
                table: "reports",
                columns: new[] { "TargetType", "TargetId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_user_consents_DocumentId",
                schema: "identity",
                table: "user_consents",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_user_consents_UserId_RecordedAt",
                schema: "identity",
                table: "user_consents",
                columns: new[] { "UserId", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_users_CreatedAt",
                schema: "identity",
                table: "users",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_users_phone_hash",
                schema: "identity",
                table: "users",
                column: "phone_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_Status_DeletionRequestedAt",
                schema: "identity",
                table: "users",
                columns: new[] { "Status", "DeletionRequestedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alert_deliveries",
                schema: "alerts");

            migrationBuilder.DropTable(
                name: "audit_log",
                schema: "moderation");

            migrationBuilder.DropTable(
                name: "conversation_participants",
                schema: "market");

            migrationBuilder.DropTable(
                name: "devices",
                schema: "alerts");

            migrationBuilder.DropTable(
                name: "messages",
                schema: "market");

            migrationBuilder.DropTable(
                name: "offers",
                schema: "market");

            migrationBuilder.DropTable(
                name: "orders",
                schema: "payments");

            migrationBuilder.DropTable(
                name: "otp_challenges",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "refresh_tokens",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "region_subscriptions",
                schema: "alerts");

            migrationBuilder.DropTable(
                name: "reports",
                schema: "moderation");

            migrationBuilder.DropTable(
                name: "user_consents",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "alerts",
                schema: "alerts");

            migrationBuilder.DropTable(
                name: "conversations",
                schema: "market");

            migrationBuilder.DropTable(
                name: "listings",
                schema: "market");

            migrationBuilder.DropTable(
                name: "consent_documents",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "regions",
                schema: "alerts");

            migrationBuilder.DropTable(
                name: "users",
                schema: "identity");
        }
    }
}
