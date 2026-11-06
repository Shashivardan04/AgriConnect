using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriConnect.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCategorySellsDaily : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SellsDaily",
                table: "Categories",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SellsDaily",
                table: "Categories");
        }
    }
}
