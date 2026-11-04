using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeschoolAI.Adapters.Migrations
{
    /// <inheritdoc />
    public partial class AddFamilyIsPremium : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_premium",
                schema: "app_identity",
                table: "families",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_premium",
                schema: "app_identity",
                table: "families");
        }
    }
}
