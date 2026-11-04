using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeschoolAI.Adapters.Migrations
{
    /// <inheritdoc />
    public partial class AddFreemiumAndPaymentEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "free_interactions_left",
                schema: "app_identity",
                table: "families",
                type: "integer",
                nullable: false,
                defaultValue: 20);

            migrationBuilder.CreateTable(
                name: "payment_events",
                schema: "app_identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    stripe_event_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    result = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    received_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_events", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_payment_events_stripe_event_id",
                schema: "app_identity",
                table: "payment_events",
                column: "stripe_event_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payment_events",
                schema: "app_identity");

            migrationBuilder.DropColumn(
                name: "free_interactions_left",
                schema: "app_identity",
                table: "families");
        }
    }
}
