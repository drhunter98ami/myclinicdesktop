using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyClinic.Migrations
{
    /// <inheritdoc />
    public partial class AddTreatmentCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "TreatmentCosts",
                type: "TEXT",
                maxLength: 3,
                nullable: false,
                defaultValue: "SYP");
            
            // Update existing records to have "SYP" as default currency
            migrationBuilder.Sql(
                "UPDATE \"TreatmentCosts\" SET \"Currency\" = 'SYP' WHERE \"Currency\" = '' OR \"Currency\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Currency",
                table: "TreatmentCosts");
        }
    }
}
