using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TattooFlash.Web.Migrations
{
    /// <inheritdoc />
    public partial class TagEntityUpdates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DispalyName",
                table: "Tag",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Tag_NormalizedName",
                table: "Tag",
                column: "NormalizedName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tag_NormalizedName",
                table: "Tag");

            migrationBuilder.DropColumn(
                name: "DispalyName",
                table: "Tag");
        }
    }
}
