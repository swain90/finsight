using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinSight.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStatementImport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CorporateFiscalYearEndDay",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 31);

            migrationBuilder.AddColumn<int>(
                name: "CorporateFiscalYearEndMonth",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 12);

            migrationBuilder.AddColumn<Guid>(
                name: "ImportBatchId",
                table: "Transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImportFingerprint",
                table: "Transactions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsTransferDismissed",
                table: "Transactions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "Transactions",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Regular");

            migrationBuilder.AddColumn<Guid>(
                name: "TransferGroupId",
                table: "Transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalAccountRef",
                table: "Accounts",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Institution",
                table: "Accounts",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Other");

            migrationBuilder.AddColumn<DateOnly>(
                name: "LatestSnapshotAsOf",
                table: "Accounts",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Owner",
                table: "Accounts",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Personal");

            migrationBuilder.CreateTable(
                name: "BalanceSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    AsOf = table.Column<DateOnly>(type: "date", nullable: false),
                    Balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ImportBatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BalanceSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BalanceSnapshots_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImportBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Institution = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: true),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: true),
                    CommittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReconciliationWarning = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MerchantRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DescriptionKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MerchantRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImportRows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    OccurredOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    MerchantName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    KindHint = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IsDuplicate = table.Column<bool>(type: "boolean", nullable: false),
                    IsExcluded = table.Column<bool>(type: "boolean", nullable: false),
                    CategoryChangedByUser = table.Column<bool>(type: "boolean", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    RawLine = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportRows_ImportBatches_ImportBatchId",
                        column: x => x.ImportBatchId,
                        principalTable: "ImportBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_AccountId_ImportFingerprint",
                table: "Transactions",
                columns: new[] { "AccountId", "ImportFingerprint" },
                unique: true,
                filter: "\"ImportFingerprint\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_ImportBatchId",
                table: "Transactions",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_Kind",
                table: "Transactions",
                column: "Kind");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_TransferGroupId",
                table: "Transactions",
                column: "TransferGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_BalanceSnapshots_AccountId_AsOf",
                table: "BalanceSnapshots",
                columns: new[] { "AccountId", "AsOf" });

            migrationBuilder.CreateIndex(
                name: "IX_BalanceSnapshots_ImportBatchId",
                table: "BalanceSnapshots",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatches_UserId_CreatedAt",
                table: "ImportBatches",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ImportRows_ImportBatchId",
                table: "ImportRows",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_MerchantRules_UserId_DescriptionKey",
                table: "MerchantRules",
                columns: new[] { "UserId", "DescriptionKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BalanceSnapshots");

            migrationBuilder.DropTable(
                name: "ImportRows");

            migrationBuilder.DropTable(
                name: "MerchantRules");

            migrationBuilder.DropTable(
                name: "ImportBatches");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_AccountId_ImportFingerprint",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_ImportBatchId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_Kind",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_TransferGroupId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "CorporateFiscalYearEndDay",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CorporateFiscalYearEndMonth",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ImportBatchId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ImportFingerprint",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "IsTransferDismissed",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "TransferGroupId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ExternalAccountRef",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "Institution",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "LatestSnapshotAsOf",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "Owner",
                table: "Accounts");
        }
    }
}
