using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MyStore.Api.Migrations
{
    public partial class AddMarketInviteTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Markets_MarketId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_MarketId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "MarketId",
                table: "Users");

            migrationBuilder.CreateTable(
                name: "MarketInvites",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    MarketId = table.Column<string>(type: "TEXT", nullable: true),
                    Key = table.Column<string>(type: "TEXT", nullable: true),
                    ExpireAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsUsed = table.Column<bool>(type: "INTEGER", nullable: false),
                    UsedByUserId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketInvites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarketInvites_Markets_MarketId",
                        column: x => x.MarketId,
                        principalTable: "Markets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MarketInvites_MarketId",
                table: "MarketInvites",
                column: "MarketId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarketInvites");

            migrationBuilder.AddColumn<string>(
                name: "MarketId",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_MarketId",
                table: "Users",
                column: "MarketId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Markets_MarketId",
                table: "Users",
                column: "MarketId",
                principalTable: "Markets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
