using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TattooFlash.Web.Migrations
{
    /// <inheritdoc />
    public partial class correctTagDisplayNameColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DispalyName",
                table: "Tags",
                newName: "DisplayName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DisplayName",
                table: "Tags",
                newName: "DispalyName");
        }
    }
}