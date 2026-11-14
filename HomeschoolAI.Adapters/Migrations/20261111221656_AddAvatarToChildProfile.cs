using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeschoolAI.Adapters.Migrations
{
    /// <inheritdoc />
    public partial class AddAvatarToChildProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "avatar_url",
                schema: "app_identity",
                table: "child_profiles",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "avatar-aguila.svg");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "avatar_url",
                schema: "app_identity",
                table: "child_profiles");
        }
    }
}
