using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriConnect.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryGroupAndSuggestedUnit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Group",
                table: "Categories",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SuggestedUnit",
                table: "Categories",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Group",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "SuggestedUnit",
                table: "Categories");
        }
    }
}
