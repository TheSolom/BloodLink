using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace BloodLink.Requests.Api.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class _20261114224128_InitialRequestsDb : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "requests");

        migrationBuilder.CreateTable(
            name: "BloodRequests",
            schema: "requests",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                HospitalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                HospitalLatitude = table.Column<double>(type: "float", nullable: false),
                HospitalLongitude = table.Column<double>(type: "float", nullable: false),
                BloodType = table.Column<string>(
                    type: "nvarchar(10)",
                    maxLength: 10,
                    nullable: false
                ),
                Component = table.Column<string>(
                    type: "nvarchar(20)",
                    maxLength: 20,
                    nullable: false
                ),
                UnitsNeeded = table.Column<int>(type: "int", nullable: false),
                Urgency = table.Column<string>(
                    type: "nvarchar(20)",
                    maxLength: 20,
                    nullable: false
                ),
                NeededBy = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                IdempotencyKey = table.Column<string>(
                    type: "nvarchar(128)",
                    maxLength: 128,
                    nullable: false
                ),
                AssignedUnitIds = table.Column<string>(type: "nvarchar(max)", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_BloodRequests", x => x.Id);
            }
        );

        migrationBuilder.CreateTable(
            name: "Hospitals",
            schema: "requests",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Latitude = table.Column<double>(type: "float", nullable: false),
                Longitude = table.Column<double>(type: "float", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Hospitals", x => x.Id);
            }
        );

        migrationBuilder.CreateTable(
            name: "InboxStates",
            schema: "requests",
            columns: table => new
            {
                Id = table
                    .Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ConsumerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                LockId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RowVersion = table.Column<byte[]>(
                    type: "rowversion",
                    rowVersion: true,
                    nullable: true
                ),
                Received = table.Column<DateTime>(type: "datetime2", nullable: false),
                ReceiveCount = table.Column<int>(type: "int", nullable: false),
                ExpirationTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                Consumed = table.Column<DateTime>(type: "datetime2", nullable: true),
                Delivered = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastSequenceNumber = table.Column<long>(type: "bigint", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_InboxStates", x => x.Id);
                table.UniqueConstraint("AK_InboxStates_MessageId_ConsumerId", x => new
                {
                    x.MessageId,
                    x.ConsumerId,
                });
            }
        );

        migrationBuilder.CreateTable(
            name: "OutboxStates",
            schema: "requests",
            columns: table => new
            {
                OutboxId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                LockId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RowVersion = table.Column<byte[]>(
                    type: "rowversion",
                    rowVersion: true,
                    nullable: true
                ),
                Created = table.Column<DateTime>(type: "datetime2", nullable: false),
                Delivered = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastSequenceNumber = table.Column<long>(type: "bigint", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OutboxStates", x => x.OutboxId);
            }
        );

        migrationBuilder.CreateTable(
            name: "StockReadModel",
            schema: "requests",
            columns: table => new
            {
                UnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BloodBankId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Latitude = table.Column<double>(type: "float", nullable: true),
                Longitude = table.Column<double>(type: "float", nullable: true),
                BloodType = table.Column<string>(
                    type: "nvarchar(10)",
                    maxLength: 10,
                    nullable: true
                ),
                Component = table.Column<string>(
                    type: "nvarchar(20)",
                    maxLength: 20,
                    nullable: true
                ),
                ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                Version = table.Column<int>(type: "int", nullable: false),
                IsComplete = table.Column<bool>(type: "bit", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StockReadModel", x => x.UnitId);
            }
        );

        migrationBuilder.CreateTable(
            name: "OutboxMessages",
            schema: "requests",
            columns: table => new
            {
                SequenceNumber = table
                    .Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                EnqueueTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                SentTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                Headers = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Properties = table.Column<string>(type: "nvarchar(max)", nullable: true),
                InboxMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                InboxConsumerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                OutboxId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ContentType = table.Column<string>(
                    type: "nvarchar(256)",
                    maxLength: 256,
                    nullable: false
                ),
                MessageType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                InitiatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                SourceAddress = table.Column<string>(
                    type: "nvarchar(256)",
                    maxLength: 256,
                    nullable: true
                ),
                DestinationAddress = table.Column<string>(
                    type: "nvarchar(256)",
                    maxLength: 256,
                    nullable: true
                ),
                ResponseAddress = table.Column<string>(
                    type: "nvarchar(256)",
                    maxLength: 256,
                    nullable: true
                ),
                FaultAddress = table.Column<string>(
                    type: "nvarchar(256)",
                    maxLength: 256,
                    nullable: true
                ),
                ExpirationTime = table.Column<DateTime>(type: "datetime2", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OutboxMessages", x => x.SequenceNumber);
                table.ForeignKey(
                    name: "FK_OutboxMessages_InboxStates_InboxMessageId_InboxConsumerId",
                    columns: x => new { x.InboxMessageId, x.InboxConsumerId },
                    principalSchema: "requests",
                    principalTable: "InboxStates",
                    principalColumns: new[] { "MessageId", "ConsumerId" }
                );
                table.ForeignKey(
                    name: "FK_OutboxMessages_OutboxStates_OutboxId",
                    column: x => x.OutboxId,
                    principalSchema: "requests",
                    principalTable: "OutboxStates",
                    principalColumn: "OutboxId"
                );
            }
        );

        migrationBuilder.InsertData(
            schema: "requests",
            table: "Hospitals",
            columns: new[] { "Id", "Name", "Latitude", "Longitude" },
            values: new object[,]
            {
                {
                    new Guid("11111111-1111-1111-1111-111111111111"),
                    "St. Thomas' Hospital",
                    51.498800000000003,
                    -0.1186,
                },
                {
                    new Guid("22222222-2222-2222-2222-222222222222"),
                    "Royal London Hospital",
                    51.518999999999998,
                    -0.059499999999999997,
                },
                {
                    new Guid("33333333-3333-3333-3333-333333333333"),
                    "King's College Hospital",
                    51.468200000000003,
                    -0.093399999999999997,
                },
            }
        );

        migrationBuilder.CreateIndex(
            name: "IX_BloodRequests_HospitalId_IdempotencyKey",
            schema: "requests",
            table: "BloodRequests",
            columns: new[] { "HospitalId", "IdempotencyKey" },
            unique: true
        );

        migrationBuilder.CreateIndex(
            name: "IX_InboxStates_Delivered",
            schema: "requests",
            table: "InboxStates",
            column: "Delivered"
        );

        migrationBuilder.CreateIndex(
            name: "IX_OutboxMessages_EnqueueTime",
            schema: "requests",
            table: "OutboxMessages",
            column: "EnqueueTime"
        );

        migrationBuilder.CreateIndex(
            name: "IX_OutboxMessages_ExpirationTime",
            schema: "requests",
            table: "OutboxMessages",
            column: "ExpirationTime"
        );

        migrationBuilder.CreateIndex(
            name: "IX_OutboxMessages_InboxMessageId_InboxConsumerId_SequenceNumber",
            schema: "requests",
            table: "OutboxMessages",
            columns: new[] { "InboxMessageId", "InboxConsumerId", "SequenceNumber" },
            unique: true,
            filter: "[InboxMessageId] IS NOT NULL AND [InboxConsumerId] IS NOT NULL"
        );

        migrationBuilder.CreateIndex(
            name: "IX_OutboxMessages_OutboxId_SequenceNumber",
            schema: "requests",
            table: "OutboxMessages",
            columns: new[] { "OutboxId", "SequenceNumber" },
            unique: true,
            filter: "[OutboxId] IS NOT NULL"
        );

        migrationBuilder.CreateIndex(
            name: "IX_OutboxStates_Created",
            schema: "requests",
            table: "OutboxStates",
            column: "Created"
        );

        migrationBuilder.CreateIndex(
            name: "IX_StockReadModel_IsComplete_Status",
            schema: "requests",
            table: "StockReadModel",
            columns: new[] { "IsComplete", "Status" }
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "BloodRequests", schema: "requests");

        migrationBuilder.DropTable(name: "Hospitals", schema: "requests");

        migrationBuilder.DropTable(name: "OutboxMessages", schema: "requests");

        migrationBuilder.DropTable(name: "StockReadModel", schema: "requests");

        migrationBuilder.DropTable(name: "InboxStates", schema: "requests");

        migrationBuilder.DropTable(name: "OutboxStates", schema: "requests");
    }
}
