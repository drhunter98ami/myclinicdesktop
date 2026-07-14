using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyClinic.Migrations
{
    /// <inheritdoc />
    public partial class AddDefaultCurrencyToAppSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DefaultCurrency",
                table: "AppSettings",
                type: "TEXT",
                maxLength: 3,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultCurrency",
                table: "AppSettings");
        }
    }
}
