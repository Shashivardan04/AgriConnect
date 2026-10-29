using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriConnect.Data.Migrations
{
    /// <inheritdoc />
    public partial class OneLabourEntryPerWorkerPerTask : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Listings_OwnerId",
                table: "Listings");

            migrationBuilder.CreateIndex(
                name: "IX_Listings_OwnerId_CategoryId",
                table: "Listings",
                columns: new[] { "OwnerId", "CategoryId" },
                unique: true,
                filter: "[Type] = 3");

            migrationBuilder.CreateIndex(
                name: "IX_Listings_Type_OwnerId_Status",
                table: "Listings",
                columns: new[] { "Type", "OwnerId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Listings_OwnerId_CategoryId",
                table: "Listings");

            migrationBuilder.DropIndex(
                name: "IX_Listings_Type_OwnerId_Status",
                table: "Listings");

            migrationBuilder.CreateIndex(
                name: "IX_Listings_OwnerId",
                table: "Listings",
                column: "OwnerId");
        }
    }
}
