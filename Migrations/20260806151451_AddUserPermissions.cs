using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFTRS.Migrations
{
    /// <inheritdoc />
    public partial class AddUserPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserPermissions",
                columns: table => new
                {
                    UserPermissionID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserID = table.Column<int>(type: "int", nullable: false),
                    Permission = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPermissions", x => x.UserPermissionID);
                    table.ForeignKey(
                        name: "FK_UserPermissions_Users_UserID",
                        column: x => x.UserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserPermissions_UserID_Permission",
                table: "UserPermissions",
                columns: new[] { "UserID", "Permission" },
                unique: true);

            migrationBuilder.Sql(@"
                INSERT INTO [UserPermissions] ([UserID], [Permission])
                SELECT [UserID], [Permission]
                FROM [Users]
                CROSS JOIN (VALUES
                    (N'Import'),
                    (N'Reconcile'),
                    (N'ResolveDiscrepancies'),
                    (N'StrategicIntelligence'),
                    (N'Templates'),
                    (N'Reports'),
                    (N'Sessions')
                ) AS permissions([Permission])
                WHERE [Role] = N'Manager';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserPermissions");
        }
    }
}
