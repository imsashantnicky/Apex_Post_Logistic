using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApexPost.Infrastructure.Tracking.Migrations
{
    /// <inheritdoc />
    public partial class AddShipmentTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TrackedShipments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrackingNumber = table.Column<string>(type: "varchar(12)", unicode: false, maxLength: 12, nullable: false),
                    WeightKg = table.Column<decimal>(type: "decimal(6,3)", nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RecipientAddress = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RecipientCity = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    RecipientName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RecipientPhone = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackedShipments", x => x.Id);
                    table.CheckConstraint("CK_TrackedShipments_Status", "[Status] IN ('Created', 'PickedUp', 'InTransit', 'OutForDelivery', 'Delivered', 'Failed')");
                    table.CheckConstraint("CK_TrackedShipments_TrackingNumber", "[TrackingNumber] LIKE 'APX-[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]'");
                    table.CheckConstraint("CK_TrackedShipments_WeightKg", "[WeightKg] > 0 AND [WeightKg] <= 30");
                });

            migrationBuilder.CreateTable(
                name: "TrackingScans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScanId = table.Column<string>(type: "varchar(42)", unicode: false, maxLength: 42, nullable: false),
                    ShipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScanType = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    CourierId = table.Column<string>(type: "varchar(6)", unicode: false, maxLength: 6, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ScannedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackingScans", x => x.Id);
                    table.CheckConstraint("CK_TrackingScans_CourierId", "[CourierId] LIKE 'CR-[0-9][0-9][0-9]'");
                    table.CheckConstraint("CK_TrackingScans_ScanId", "[ScanId] LIKE 'scan::%'");
                    table.CheckConstraint("CK_TrackingScans_ScanType", "[ScanType] IN ('Pickup', 'InTransit', 'Delivered', 'Failed')");
                    table.ForeignKey(
                        name: "FK_TrackingScans_TrackedShipments_ShipmentId",
                        column: x => x.ShipmentId,
                        principalTable: "TrackedShipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TrackedShipments_Status",
                table: "TrackedShipments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TrackedShipments_TrackingNumber",
                table: "TrackedShipments",
                column: "TrackingNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrackingScans_ScanId",
                table: "TrackingScans",
                column: "ScanId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrackingScans_ShipmentId_ScannedAtUtc",
                table: "TrackingScans",
                columns: new[] { "ShipmentId", "ScannedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrackingScans");

            migrationBuilder.DropTable(
                name: "TrackedShipments");
        }
    }
}
