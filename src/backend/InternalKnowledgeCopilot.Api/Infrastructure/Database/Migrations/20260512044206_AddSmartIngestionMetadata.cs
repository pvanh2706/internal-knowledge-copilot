using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddSmartIngestionMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "document_summary",
                table: "document_versions",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "normalized_text_path",
                table: "document_versions",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "processing_warnings_json",
                table: "document_versions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "section_count",
                table: "document_versions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "section_title",
                table: "ai_interaction_sources",
                type: "TEXT",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "document_summary",
                table: "document_versions");

            migrationBuilder.DropColumn(
                name: "normalized_text_path",
                table: "document_versions");

            migrationBuilder.DropColumn(
                name: "processing_warnings_json",
                table: "document_versions");

            migrationBuilder.DropColumn(
                name: "section_count",
                table: "document_versions");

            migrationBuilder.DropColumn(
                name: "section_title",
                table: "ai_interaction_sources");
        }
    }
}
