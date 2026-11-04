using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeschoolAI.Adapters.Migrations
{
    /// <inheritdoc />
    public partial class AddChildProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "child_progress",
                schema: "app_identity",
                columns: table => new
                {
                    child_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    total_xp = table.Column<int>(type: "integer", nullable: false),
                    current_level = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_child_progress", x => x.child_profile_id);
                    table.ForeignKey(
                        name: "FK_child_progress_child_profiles_child_profile_id",
                        column: x => x.child_profile_id,
                        principalSchema: "app_identity",
                        principalTable: "child_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "child_progress",
                schema: "app_identity");
        }
    }
}
