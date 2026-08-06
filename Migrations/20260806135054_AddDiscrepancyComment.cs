using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFTRS.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscrepancyComment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DiscrepancyComment",
                table: "Staging_Transactions",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscrepancyComment",
                table: "Staging_Transactions");
        }
    }
}
