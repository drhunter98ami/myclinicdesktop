using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyClinic.Migrations
{
    /// <inheritdoc />
    public partial class UpdateModelForPendingChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "UsdToSypRateSnapshot",
                table: "Visits",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UsdToSypRateSnapshot",
                table: "Visits");
        }
    }
}
