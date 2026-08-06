using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFTRS.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionReconciledAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ReconciledAt",
                table: "ReconciliationSessions",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReconciledAt",
                table: "ReconciliationSessions");
        }
    }
}
