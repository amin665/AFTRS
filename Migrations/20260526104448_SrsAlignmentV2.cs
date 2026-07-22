using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFTRS.Migrations
{
    /// <inheritdoc />
    public partial class SrsAlignmentV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FinancialAuditLogs_Transactions_TransactionID",
                table: "FinancialAuditLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_FinancialAuditLogs_Users_UserID",
                table: "FinancialAuditLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Categories_CategoryID",
                table: "Transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Transactions_MatchedTransactionID",
                table: "Transactions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Transactions",
                table: "Transactions");

            migrationBuilder.RenameTable(
                name: "Transactions",
                newName: "Staging_Transactions");

            migrationBuilder.RenameIndex(
                name: "IX_Transactions_MatchedTransactionID",
                table: "Staging_Transactions",
                newName: "IX_Staging_Transactions_MatchedTransactionID");

            migrationBuilder.RenameIndex(
                name: "IX_Transactions_CategoryID",
                table: "Staging_Transactions",
                newName: "IX_Staging_Transactions_CategoryID");

            migrationBuilder.AddColumn<string>(
                name: "MatchMethod",
                table: "Staging_Transactions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Staging_Transactions",
                table: "Staging_Transactions",
                column: "TransactionID");

            migrationBuilder.AddForeignKey(
                name: "FK_FinancialAuditLogs_Staging_Transactions_TransactionID",
                table: "FinancialAuditLogs",
                column: "TransactionID",
                principalTable: "Staging_Transactions",
                principalColumn: "TransactionID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FinancialAuditLogs_Users_UserID",
                table: "FinancialAuditLogs",
                column: "UserID",
                principalTable: "Users",
                principalColumn: "UserID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Staging_Transactions_Categories_CategoryID",
                table: "Staging_Transactions",
                column: "CategoryID",
                principalTable: "Categories",
                principalColumn: "CategoryID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Staging_Transactions_Staging_Transactions_MatchedTransactionID",
                table: "Staging_Transactions",
                column: "MatchedTransactionID",
                principalTable: "Staging_Transactions",
                principalColumn: "TransactionID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FinancialAuditLogs_Staging_Transactions_TransactionID",
                table: "FinancialAuditLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_FinancialAuditLogs_Users_UserID",
                table: "FinancialAuditLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_Staging_Transactions_Categories_CategoryID",
                table: "Staging_Transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Staging_Transactions_Staging_Transactions_MatchedTransactionID",
                table: "Staging_Transactions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Staging_Transactions",
                table: "Staging_Transactions");

            migrationBuilder.DropColumn(
                name: "MatchMethod",
                table: "Staging_Transactions");

            migrationBuilder.RenameTable(
                name: "Staging_Transactions",
                newName: "Transactions");

            migrationBuilder.RenameIndex(
                name: "IX_Staging_Transactions_MatchedTransactionID",
                table: "Transactions",
                newName: "IX_Transactions_MatchedTransactionID");

            migrationBuilder.RenameIndex(
                name: "IX_Staging_Transactions_CategoryID",
                table: "Transactions",
                newName: "IX_Transactions_CategoryID");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Transactions",
                table: "Transactions",
                column: "TransactionID");

            migrationBuilder.AddForeignKey(
                name: "FK_FinancialAuditLogs_Transactions_TransactionID",
                table: "FinancialAuditLogs",
                column: "TransactionID",
                principalTable: "Transactions",
                principalColumn: "TransactionID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FinancialAuditLogs_Users_UserID",
                table: "FinancialAuditLogs",
                column: "UserID",
                principalTable: "Users",
                principalColumn: "UserID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Categories_CategoryID",
                table: "Transactions",
                column: "CategoryID",
                principalTable: "Categories",
                principalColumn: "CategoryID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Transactions_MatchedTransactionID",
                table: "Transactions",
                column: "MatchedTransactionID",
                principalTable: "Transactions",
                principalColumn: "TransactionID",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
