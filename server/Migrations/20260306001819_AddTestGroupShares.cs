using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Migrations
{
    /// <inheritdoc />
    public partial class AddTestGroupShares : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "Tests");

            migrationBuilder.CreateTable(
                name: "TestGroupShares",
                columns: table => new
                {
                    TestId = table.Column<int>(type: "integer", nullable: false),
                    GroupId = table.Column<int>(type: "integer", nullable: false),
                    SharedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestGroupShares", x => new { x.TestId, x.GroupId });
                    table.ForeignKey(
                        name: "FK_TestGroupShares_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TestGroupShares_Tests_TestId",
                        column: x => x.TestId,
                        principalTable: "Tests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UsersFiles_UserId",
                table: "UsersFiles",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TestGroupShares_GroupId",
                table: "TestGroupShares",
                column: "GroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_UsersFiles_Users_UserId",
                table: "UsersFiles",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UsersFiles_Users_UserId",
                table: "UsersFiles");

            migrationBuilder.DropTable(
                name: "TestGroupShares");

            migrationBuilder.DropIndex(
                name: "IX_UsersFiles_UserId",
                table: "UsersFiles");

            migrationBuilder.AddColumn<int>(
                name: "GroupId",
                table: "Tests",
                type: "integer",
                nullable: true);
        }
    }
}
