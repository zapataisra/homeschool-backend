using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeschoolAI.Adapters.Migrations
{
    /// <inheritdoc />
    public partial class AddChildTopicProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "child_topic_progress",
                schema: "app_identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    child_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    topic_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    topic_title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    campo_formativo = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    time_spent_seconds = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    badge_earned = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_activity_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_child_topic_progress", x => x.id);
                    table.ForeignKey(
                        name: "FK_child_topic_progress_child_profiles_child_profile_id",
                        column: x => x.child_profile_id,
                        principalSchema: "app_identity",
                        principalTable: "child_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_child_topic_progress_child_profile_id_topic_id",
                schema: "app_identity",
                table: "child_topic_progress",
                columns: new[] { "child_profile_id", "topic_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "child_topic_progress",
                schema: "app_identity");
        }
    }
}
