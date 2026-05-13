using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentUnderstandingMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "document_type",
                table: "document_versions",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "effective_date",
                table: "document_versions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entities_json",
                table: "document_versions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "key_topics_json",
                table: "document_versions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "language",
                table: "document_versions",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "quality_warnings_json",
                table: "document_versions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sensitivity",
                table: "document_versions",
                type: "TEXT",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "document_type",
                table: "document_versions");

            migrationBuilder.DropColumn(
                name: "effective_date",
                table: "document_versions");

            migrationBuilder.DropColumn(
                name: "entities_json",
                table: "document_versions");

            migrationBuilder.DropColumn(
                name: "key_topics_json",
                table: "document_versions");

            migrationBuilder.DropColumn(
                name: "language",
                table: "document_versions");

            migrationBuilder.DropColumn(
                name: "quality_warnings_json",
                table: "document_versions");

            migrationBuilder.DropColumn(
                name: "sensitivity",
                table: "document_versions");
        }
    }
}
