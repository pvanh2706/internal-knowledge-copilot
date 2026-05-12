using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddStructuredAiAnswers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "confidence",
                table: "ai_interactions",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "low");

            migrationBuilder.AddColumn<string>(
                name: "conflicts_json",
                table: "ai_interactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "missing_information_json",
                table: "ai_interactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "suggested_follow_ups_json",
                table: "ai_interactions",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "confidence",
                table: "ai_interactions");

            migrationBuilder.DropColumn(
                name: "conflicts_json",
                table: "ai_interactions");

            migrationBuilder.DropColumn(
                name: "missing_information_json",
                table: "ai_interactions");

            migrationBuilder.DropColumn(
                name: "suggested_follow_ups_json",
                table: "ai_interactions");
        }
    }
}
