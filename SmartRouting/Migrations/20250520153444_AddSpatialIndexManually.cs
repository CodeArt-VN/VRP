using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace SmartRouting.Migrations
{
    /// <inheritdoc />
    public partial class AddSpatialIndexManually : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tbl_001",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    District = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Province = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Ward = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Street = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Address1 = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Location = table.Column<Point>(type: "geography", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_001", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_002",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Loc1 = table.Column<int>(type: "int", nullable: false),
                    Loc2 = table.Column<int>(type: "int", nullable: false),
                    Distance = table.Column<double>(type: "float", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_002", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tbl_002_tbl_001_Loc1",
                        column: x => x.Loc1,
                        principalTable: "tbl_001",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tbl_002_tbl_001_Loc2",
                        column: x => x.Loc2,
                        principalTable: "tbl_001",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tbl_002_Loc1_Loc2",
                table: "tbl_002",
                columns: new[] { "Loc1", "Loc2" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_002_Loc2",
                table: "tbl_002",
                column: "Loc2");

            // Manually add the spatial index for the Location column in tbl_001
            migrationBuilder.Sql("CREATE SPATIAL INDEX IX_tbl_001_Location ON tbl_001(Location);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Manually drop the spatial index
            migrationBuilder.Sql("DROP INDEX IX_tbl_001_Location ON tbl_001;");

            migrationBuilder.DropTable(
                name: "tbl_002");

            migrationBuilder.DropTable(
                name: "tbl_001");
        }
    }
}
