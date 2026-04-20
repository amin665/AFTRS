using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFTRS.Migrations
{
    /// <inheritdoc />
    public partial class AddBatchNameToAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BatchName",
                table: "FinancialAuditLogs",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BatchName",
                table: "FinancialAuditLogs");
        }
    }
}
