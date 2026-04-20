using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFTRS.Migrations
{
    /// <inheritdoc />
    public partial class AddBatchSystemFinal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_ReconciliationBatch_BatchId",
                table: "Transactions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ReconciliationBatch",
                table: "ReconciliationBatch");

            migrationBuilder.RenameTable(
                name: "ReconciliationBatch",
                newName: "ReconciliationBatches");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ReconciliationBatches",
                table: "ReconciliationBatches",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_ReconciliationBatches_BatchId",
                table: "Transactions",
                column: "BatchId",
                principalTable: "ReconciliationBatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_ReconciliationBatches_BatchId",
                table: "Transactions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ReconciliationBatches",
                table: "ReconciliationBatches");

            migrationBuilder.RenameTable(
                name: "ReconciliationBatches",
                newName: "ReconciliationBatch");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ReconciliationBatch",
                table: "ReconciliationBatch",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_ReconciliationBatch_BatchId",
                table: "Transactions",
                column: "BatchId",
                principalTable: "ReconciliationBatch",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
