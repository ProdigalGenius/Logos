using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SahaNet.Modules.Scrap.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "scrap");

            migrationBuilder.CreateTable(
                name: "materials",
                schema: "scrap",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Group = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_materials", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "pickups",
                schema: "scrap",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequesterId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProvinceCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    District = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    address_encrypted = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    MaterialCodes = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EstimatedKg = table.Column<decimal>(type: "numeric(10,1)", precision: 10, scale: 1, nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CollectorId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClaimedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pickups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "price_reports",
                schema: "scrap",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ProvinceCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    PricePerKg = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    ReportedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_price_reports", x => x.Id);
                });

            migrationBuilder.InsertData(
                schema: "scrap",
                table: "materials",
                columns: new[] { "Code", "Group", "Name" },
                values: new object[,]
                {
                    { "aku", "tehlikeli", "Akü" },
                    { "aluminyum", "metal", "Alüminyum" },
                    { "bakir", "metal", "Bakır" },
                    { "beyaz-esya", "elektronik", "Beyaz eşya" },
                    { "demir", "metal", "Demir / çelik" },
                    { "elektronik", "elektronik", "Elektronik atık" },
                    { "kablo", "metal", "Kablo" },
                    { "karton", "kağıt", "Kağıt / karton" },
                    { "krom", "metal", "Krom / paslanmaz" },
                    { "plastik", "plastik", "Plastik" },
                    { "sari", "metal", "Sarı (pirinç)" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_pickups_CollectorId",
                schema: "scrap",
                table: "pickups",
                column: "CollectorId");

            migrationBuilder.CreateIndex(
                name: "IX_pickups_RequesterId",
                schema: "scrap",
                table: "pickups",
                column: "RequesterId");

            migrationBuilder.CreateIndex(
                name: "IX_pickups_Status_ProvinceCode_CreatedAt",
                schema: "scrap",
                table: "pickups",
                columns: new[] { "Status", "ProvinceCode", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_price_reports_Date_MaterialCode_ProvinceCode",
                schema: "scrap",
                table: "price_reports",
                columns: new[] { "Date", "MaterialCode", "ProvinceCode" });

            migrationBuilder.CreateIndex(
                name: "IX_price_reports_ReportedBy_Date_MaterialCode_ProvinceCode",
                schema: "scrap",
                table: "price_reports",
                columns: new[] { "ReportedBy", "Date", "MaterialCode", "ProvinceCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "materials",
                schema: "scrap");

            migrationBuilder.DropTable(
                name: "pickups",
                schema: "scrap");

            migrationBuilder.DropTable(
                name: "price_reports",
                schema: "scrap");
        }
    }
}
