using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRouting.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAddressIndexDistanceRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tbl_002_tbl_001_Loc1",
                table: "tbl_002");

            migrationBuilder.DropForeignKey(
                name: "FK_tbl_002_tbl_001_Loc2",
                table: "tbl_002");

            migrationBuilder.DropIndex(
                name: "IX_tbl_002_Loc2",
                table: "tbl_002");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_tbl_002_Loc2",
                table: "tbl_002",
                column: "Loc2");

            migrationBuilder.AddForeignKey(
                name: "FK_tbl_002_tbl_001_Loc1",
                table: "tbl_002",
                column: "Loc1",
                principalTable: "tbl_001",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_tbl_002_tbl_001_Loc2",
                table: "tbl_002",
                column: "Loc2",
                principalTable: "tbl_001",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
