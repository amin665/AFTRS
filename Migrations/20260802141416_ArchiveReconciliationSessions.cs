using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFTRS.Migrations
{
    /// <inheritdoc />
    public partial class ArchiveReconciliationSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SessionID",
                table: "Staging_Transactions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SessionID",
                table: "FinancialAuditLogs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SessionID",
                table: "FileUploadRecords",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SessionID",
                table: "BudgetTargets",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ReconciliationSessions",
                columns: table => new
                {
                    SessionID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ArchivedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserID = table.Column<int>(type: "int", nullable: true),
                    ArchivedByUserID = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReconciliationSessions", x => x.SessionID);
                });

            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM [Staging_Transactions])
                    OR EXISTS (SELECT 1 FROM [FileUploadRecords])
                    OR EXISTS (SELECT 1 FROM [FinancialAuditLogs])
                    OR EXISTS (SELECT 1 FROM [BudgetTargets])
                BEGIN
                    INSERT INTO [ReconciliationSessions] ([Name], [Status], [CreatedAt], [ArchivedAt])
                    VALUES (N'Legacy Session', N'Archived', GETDATE(), GETDATE());

                    DECLARE @LegacySessionId int = SCOPE_IDENTITY();
                    UPDATE [Staging_Transactions] SET [SessionID] = @LegacySessionId WHERE [SessionID] IS NULL;
                    UPDATE [FileUploadRecords] SET [SessionID] = @LegacySessionId WHERE [SessionID] IS NULL;
                    UPDATE [FinancialAuditLogs] SET [SessionID] = @LegacySessionId WHERE [SessionID] IS NULL;
                    UPDATE [BudgetTargets] SET [SessionID] = @LegacySessionId WHERE [SessionID] IS NULL;
                END");

            migrationBuilder.AlterColumn<int>(
                name: "SessionID",
                table: "Staging_Transactions",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "SessionID",
                table: "FinancialAuditLogs",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "SessionID",
                table: "FileUploadRecords",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "SessionID",
                table: "BudgetTargets",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Staging_Transactions_SessionID",
                table: "Staging_Transactions",
                column: "SessionID");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialAuditLogs_SessionID",
                table: "FinancialAuditLogs",
                column: "SessionID");

            migrationBuilder.CreateIndex(
                name: "IX_FileUploadRecords_SessionID",
                table: "FileUploadRecords",
                column: "SessionID");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetTargets_SessionID",
                table: "BudgetTargets",
                column: "SessionID");

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetTargets_ReconciliationSessions_SessionID",
                table: "BudgetTargets",
                column: "SessionID",
                principalTable: "ReconciliationSessions",
                principalColumn: "SessionID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FileUploadRecords_ReconciliationSessions_SessionID",
                table: "FileUploadRecords",
                column: "SessionID",
                principalTable: "ReconciliationSessions",
                principalColumn: "SessionID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FinancialAuditLogs_ReconciliationSessions_SessionID",
                table: "FinancialAuditLogs",
                column: "SessionID",
                principalTable: "ReconciliationSessions",
                principalColumn: "SessionID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Staging_Transactions_ReconciliationSessions_SessionID",
                table: "Staging_Transactions",
                column: "SessionID",
                principalTable: "ReconciliationSessions",
                principalColumn: "SessionID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BudgetTargets_ReconciliationSessions_SessionID",
                table: "BudgetTargets");

            migrationBuilder.DropForeignKey(
                name: "FK_FileUploadRecords_ReconciliationSessions_SessionID",
                table: "FileUploadRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_FinancialAuditLogs_ReconciliationSessions_SessionID",
                table: "FinancialAuditLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_Staging_Transactions_ReconciliationSessions_SessionID",
                table: "Staging_Transactions");

            migrationBuilder.DropTable(
                name: "ReconciliationSessions");

            migrationBuilder.DropIndex(
                name: "IX_Staging_Transactions_SessionID",
                table: "Staging_Transactions");

            migrationBuilder.DropIndex(
                name: "IX_FinancialAuditLogs_SessionID",
                table: "FinancialAuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_FileUploadRecords_SessionID",
                table: "FileUploadRecords");

            migrationBuilder.DropIndex(
                name: "IX_BudgetTargets_SessionID",
                table: "BudgetTargets");

            migrationBuilder.DropColumn(
                name: "SessionID",
                table: "Staging_Transactions");

            migrationBuilder.DropColumn(
                name: "SessionID",
                table: "FinancialAuditLogs");

            migrationBuilder.DropColumn(
                name: "SessionID",
                table: "FileUploadRecords");

            migrationBuilder.DropColumn(
                name: "SessionID",
                table: "BudgetTargets");
        }
    }
}
