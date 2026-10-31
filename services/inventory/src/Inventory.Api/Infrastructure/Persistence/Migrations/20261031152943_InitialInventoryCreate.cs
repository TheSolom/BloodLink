using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodLink.Inventory.Api.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class _20261031152943_InitialInventoryCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "inventory");

        migrationBuilder.CreateTable(
            name: "BloodBanks",
            schema: "inventory",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Latitude = table.Column<double>(type: "float", nullable: false),
                Longitude = table.Column<double>(type: "float", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_BloodBanks", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "BloodUnits",
            schema: "inventory",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DonationNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                BloodType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                Component = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                CollectedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                BloodBankId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CurrentReservationRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Version = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_BloodUnits", x => x.Id);
                table.ForeignKey(
                    name: "FK_BloodUnits_BloodBanks_BloodBankId",
                    column: x => x.BloodBankId,
                    principalSchema: "inventory",
                    principalTable: "BloodBanks",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_BloodUnits_BloodBankId_DonationNumber",
            schema: "inventory",
            table: "BloodUnits",
            columns: new[] { "BloodBankId", "DonationNumber" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_BloodUnits_Status_BloodType_Component_ExpiresAt",
            schema: "inventory",
            table: "BloodUnits",
            columns: new[] { "Status", "BloodType", "Component", "ExpiresAt" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "BloodUnits",
            schema: "inventory");

        migrationBuilder.DropTable(
            name: "BloodBanks",
            schema: "inventory");
    }
}
