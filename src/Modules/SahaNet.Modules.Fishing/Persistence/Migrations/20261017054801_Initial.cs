using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SahaNet.Modules.Fishing.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "fishing");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.CreateTable(
                name: "bans",
                schema: "fishing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StartMonth = table.Column<int>(type: "integer", nullable: false),
                    StartDay = table.Column<int>(type: "integer", nullable: false),
                    EndMonth = table.Column<int>(type: "integer", nullable: false),
                    EndDay = table.Column<int>(type: "integer", nullable: false),
                    SpeciesCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    RegionCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Gear = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "catches",
                schema: "fishing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaughtAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SpeciesCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    WeightKg = table.Column<double>(type: "double precision", nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: true),
                    LengthCm = table.Column<double>(type: "double precision", nullable: true),
                    SpotId = table.Column<Guid>(type: "uuid", nullable: true),
                    PortId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "market_prices",
                schema: "fishing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SpeciesCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ProvinceCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    PricePerKg = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    ReportedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_market_prices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ports",
                schema: "fishing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ProvinceCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Location = table.Column<Point>(type: "geography (point, 4326)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "species",
                schema: "fishing",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ScientificName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    MinLengthCm = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_species", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "spots",
                schema: "fishing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    location_encrypted = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DepthM = table.Column<double>(type: "double precision", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SpeciesCodes = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_spots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "spot_shares",
                schema: "fishing",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    SpotId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SharedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_spot_shares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_spot_shares_spots_SpotId",
                        column: x => x.SpotId,
                        principalSchema: "fishing",
                        principalTable: "spots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "fishing",
                table: "bans",
                columns: new[] { "Id", "EndDay", "EndMonth", "Gear", "Notes", "RegionCode", "SpeciesCode", "StartDay", "StartMonth", "Title" },
                values: new object[,]
                {
                    { new Guid("f15eba40-0000-4000-8000-000000000001"), 31, 8, "Gırgır, trol", "TASLAK — Denizlerde gırgır ve trol ile avcılık yasak. Amatör avcılık kuralları ayrıdır.", null, null, 15, 4, "Genel av yasağı (gırgır ve trol)" },
                    { new Guid("f15eba40-0000-4000-8000-000000000002"), 15, 6, null, "TASLAK — Karadeniz'de kalkan avı yasak dönem.", null, "kalkan", 15, 4, "Kalkan av yasağı (Karadeniz)" },
                    { new Guid("f15eba40-0000-4000-8000-000000000003"), 31, 12, "Zıpkın (tüplü)", "TASLAK — Tüplü dalışla zıpkın avı her zaman yasak.", null, "orfoz", 1, 1, "Orfoz ve lagos koruma" },
                    { new Guid("f15eba40-0000-4000-8000-000000000004"), 31, 8, null, "TASLAK — Dönem ve kapsam tebliğe göre doğrulanmalı.", null, "lufer", 1, 5, "Lüfer küçük boy (çinekop) koruma" }
                });

            migrationBuilder.InsertData(
                schema: "fishing",
                table: "ports",
                columns: new[] { "Id", "Code", "Location", "Name", "ProvinceCode" },
                values: new object[,]
                {
                    { new Guid("f15e0000-0000-4000-8000-000000000001"), "rumelifeneri", (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (29.111 41.234)"), "Rumelifeneri", "34" },
                    { new Guid("f15e0000-0000-4000-8000-000000000002"), "sariyer", (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (29.057 41.169)"), "Sarıyer", "34" },
                    { new Guid("f15e0000-0000-4000-8000-000000000003"), "kumkapi", (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (28.965 41.001)"), "Kumkapı", "34" },
                    { new Guid("f15e0000-0000-4000-8000-000000000004"), "tuzla", (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (29.299 40.815)"), "Tuzla", "34" },
                    { new Guid("f15e0000-0000-4000-8000-000000000005"), "sile", (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (29.611 41.179)"), "Şile", "34" },
                    { new Guid("f15e0000-0000-4000-8000-000000000006"), "tekirdag", (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (27.515 40.975)"), "Tekirdağ", "59" },
                    { new Guid("f15e0000-0000-4000-8000-000000000007"), "erdek", (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (27.793 40.397)"), "Erdek", "10" },
                    { new Guid("f15e0000-0000-4000-8000-000000000008"), "canakkale", (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (26.405 40.153)"), "Çanakkale", "17" },
                    { new Guid("f15e0000-0000-4000-8000-000000000009"), "ayvalik", (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (26.693 39.319)"), "Ayvalık", "10" },
                    { new Guid("f15e0000-0000-4000-8000-000000000010"), "foca", (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (26.754 38.67)"), "Foça", "35" },
                    { new Guid("f15e0000-0000-4000-8000-000000000011"), "cesme", (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (26.303 38.324)"), "Çeşme", "35" },
                    { new Guid("f15e0000-0000-4000-8000-000000000012"), "bodrum", (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (27.424 37.034)"), "Bodrum", "48" },
                    { new Guid("f15e0000-0000-4000-8000-000000000013"), "fethiye", (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (29.103 36.623)"), "Fethiye", "48" },
                    { new Guid("f15e0000-0000-4000-8000-000000000014"), "antalya", (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (30.704 36.884)"), "Antalya", "07" },
                    { new Guid("f15e0000-0000-4000-8000-000000000015"), "mersin", (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (34.621 36.795)"), "Mersin", "33" },
                    { new Guid("f15e0000-0000-4000-8000-000000000016"), "iskenderun", (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (36.176 36.592)"), "İskenderun", "31" },
                    { new Guid("f15e0000-0000-4000-8000-000000000017"), "eregli", (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (31.418 41.283)"), "Karadeniz Ereğli", "67" },
                    { new Guid("f15e0000-0000-4000-8000-000000000018"), "amasra", (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (32.387 41.748)"), "Amasra", "74" },
                    { new Guid("f15e0000-0000-4000-8000-000000000019"), "sinop", (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (35.153 42.023)"), "Sinop", "57" },
                    { new Guid("f15e0000-0000-4000-8000-000000000020"), "samsun", (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (36.34 41.293)"), "Samsun", "55" },
                    { new Guid("f15e0000-0000-4000-8000-000000000021"), "trabzon", (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (39.736 41.005)"), "Trabzon", "61" },
                    { new Guid("f15e0000-0000-4000-8000-000000000022"), "rize", (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (40.52 41.028)"), "Rize", "53" }
                });

            migrationBuilder.InsertData(
                schema: "fishing",
                table: "species",
                columns: new[] { "Code", "MinLengthCm", "Name", "ScientificName" },
                values: new object[,]
                {
                    { "ahtapot", null, "Ahtapot", "Octopus vulgaris" },
                    { "barbunya", 13, "Barbunya", "Mullus barbatus" },
                    { "cipura", 20, "Çipura", "Sparus aurata" },
                    { "diger", null, "Diğer", null },
                    { "dil", 20, "Dil", "Solea solea" },
                    { "hamsi", 9, "Hamsi", "Engraulis encrasicolus" },
                    { "istakoz", 25, "Istakoz", "Homarus gammarus" },
                    { "istavrit", 13, "İstavrit", "Trachurus mediterraneus" },
                    { "kalamar", null, "Kalamar", "Loligo vulgaris" },
                    { "kalkan", 45, "Kalkan", "Scophthalmus maximus" },
                    { "kefal", 20, "Kefal", "Mugil cephalus" },
                    { "kilic", 125, "Kılıç", "Xiphias gladius" },
                    { "kolyoz", 18, "Kolyoz", "Scomber colias" },
                    { "lagos", 45, "Lagos", "Epinephelus aeneus" },
                    { "levrek", 25, "Levrek", "Dicentrarchus labrax" },
                    { "lufer", 20, "Lüfer", "Pomatomus saltatrix" },
                    { "mercan", 15, "Mercan", "Pagellus erythrinus" },
                    { "mezgit", 13, "Mezgit", "Merlangius merlangus" },
                    { "orfoz", 45, "Orfoz", "Epinephelus marginatus" },
                    { "palamut", 25, "Palamut", "Sarda sarda" },
                    { "sardalya", 11, "Sardalya", "Sardina pilchardus" },
                    { "sinarit", 20, "Sinarit", "Dentex dentex" },
                    { "tekir", 13, "Tekir", "Mullus surmuletus" },
                    { "uskumru", 18, "Uskumru", "Scomber scombrus" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_catches_UserId_CaughtAt",
                schema: "fishing",
                table: "catches",
                columns: new[] { "UserId", "CaughtAt" });

            migrationBuilder.CreateIndex(
                name: "IX_market_prices_Date_SpeciesCode_ProvinceCode",
                schema: "fishing",
                table: "market_prices",
                columns: new[] { "Date", "SpeciesCode", "ProvinceCode" });

            migrationBuilder.CreateIndex(
                name: "IX_market_prices_ReportedBy_Date_SpeciesCode_ProvinceCode",
                schema: "fishing",
                table: "market_prices",
                columns: new[] { "ReportedBy", "Date", "SpeciesCode", "ProvinceCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ports_Code",
                schema: "fishing",
                table: "ports",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_spot_shares_SpotId_UserId",
                schema: "fishing",
                table: "spot_shares",
                columns: new[] { "SpotId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_spot_shares_UserId",
                schema: "fishing",
                table: "spot_shares",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_spots_OwnerId",
                schema: "fishing",
                table: "spots",
                column: "OwnerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bans",
                schema: "fishing");

            migrationBuilder.DropTable(
                name: "catches",
                schema: "fishing");

            migrationBuilder.DropTable(
                name: "market_prices",
                schema: "fishing");

            migrationBuilder.DropTable(
                name: "ports",
                schema: "fishing");

            migrationBuilder.DropTable(
                name: "species",
                schema: "fishing");

            migrationBuilder.DropTable(
                name: "spot_shares",
                schema: "fishing");

            migrationBuilder.DropTable(
                name: "spots",
                schema: "fishing");
        }
    }
}
