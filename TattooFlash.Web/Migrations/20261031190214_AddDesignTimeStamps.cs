using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TattooFlash.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddDesignTimeStamps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "Created",
                table: "Designs",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(2026, 10, 31, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.AddColumn<DateTime>(
                name: "Updated",
                table: "Designs",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(2026, 10, 31, 0, 0, 0, 0, DateTimeKind.Utc));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Created",
                table: "Designs");

            migrationBuilder.DropColumn(
                name: "Updated",
                table: "Designs");
        }
    }
}
