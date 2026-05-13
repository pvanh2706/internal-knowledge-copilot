using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeChunksLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "knowledge_chunks",
                columns: table => new
                {
                    chunk_id = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    source_type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    source_id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    document_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    document_version_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    wiki_page_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    correction_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    folder_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    visibility_scope = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    folder_path = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    section_title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    section_index = table.Column<int>(type: "INTEGER", nullable: true),
                    chunk_index = table.Column<int>(type: "INTEGER", nullable: false),
                    text = table.Column<string>(type: "TEXT", nullable: false),
                    text_hash = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    vector_id = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    metadata_json = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_chunks", x => x.chunk_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_chunks_document_id",
                table: "knowledge_chunks",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_chunks_document_version_id",
                table: "knowledge_chunks",
                column: "document_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_chunks_folder_id",
                table: "knowledge_chunks",
                column: "folder_id");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_chunks_source_type_source_id",
                table: "knowledge_chunks",
                columns: new[] { "source_type", "source_id" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_chunks_source_type_status",
                table: "knowledge_chunks",
                columns: new[] { "source_type", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_chunks_text_hash",
                table: "knowledge_chunks",
                column: "text_hash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "knowledge_chunks");
        }
    }
}
