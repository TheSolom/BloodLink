using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodLink.Inventory.Api.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class _20261113141739_AddMassTransitOutbox : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "InboxStates",
            schema: "inventory",
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
                table.UniqueConstraint(
                    "AK_InboxStates_MessageId_ConsumerId",
                    x => new { x.MessageId, x.ConsumerId }
                );
            }
        );

        migrationBuilder.CreateTable(
            name: "OutboxStates",
            schema: "inventory",
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
            name: "OutboxMessages",
            schema: "inventory",
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
                    principalSchema: "inventory",
                    principalTable: "InboxStates",
                    principalColumns: new[] { "MessageId", "ConsumerId" }
                );
                table.ForeignKey(
                    name: "FK_OutboxMessages_OutboxStates_OutboxId",
                    column: x => x.OutboxId,
                    principalSchema: "inventory",
                    principalTable: "OutboxStates",
                    principalColumn: "OutboxId"
                );
            }
        );

        migrationBuilder.CreateIndex(
            name: "IX_InboxStates_Delivered",
            schema: "inventory",
            table: "InboxStates",
            column: "Delivered"
        );

        migrationBuilder.CreateIndex(
            name: "IX_OutboxMessages_EnqueueTime",
            schema: "inventory",
            table: "OutboxMessages",
            column: "EnqueueTime"
        );

        migrationBuilder.CreateIndex(
            name: "IX_OutboxMessages_ExpirationTime",
            schema: "inventory",
            table: "OutboxMessages",
            column: "ExpirationTime"
        );

        migrationBuilder.CreateIndex(
            name: "IX_OutboxMessages_InboxMessageId_InboxConsumerId_SequenceNumber",
            schema: "inventory",
            table: "OutboxMessages",
            columns: new[] { "InboxMessageId", "InboxConsumerId", "SequenceNumber" },
            unique: true,
            filter: "[InboxMessageId] IS NOT NULL AND [InboxConsumerId] IS NOT NULL"
        );

        migrationBuilder.CreateIndex(
            name: "IX_OutboxMessages_OutboxId_SequenceNumber",
            schema: "inventory",
            table: "OutboxMessages",
            columns: new[] { "OutboxId", "SequenceNumber" },
            unique: true,
            filter: "[OutboxId] IS NOT NULL"
        );

        migrationBuilder.CreateIndex(
            name: "IX_OutboxStates_Created",
            schema: "inventory",
            table: "OutboxStates",
            column: "Created"
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "OutboxMessages", schema: "inventory");

        migrationBuilder.DropTable(name: "InboxStates", schema: "inventory");

        migrationBuilder.DropTable(name: "OutboxStates", schema: "inventory");
    }
}
