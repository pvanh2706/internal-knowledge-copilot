using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddAiInteractions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_interactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    question = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    answer = table.Column<string>(type: "TEXT", nullable: false),
                    scope_type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    scope_folder_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    scope_document_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    needs_clarification = table.Column<bool>(type: "INTEGER", nullable: false),
                    latency_ms = table.Column<int>(type: "INTEGER", nullable: false),
                    used_wiki_count = table.Column<int>(type: "INTEGER", nullable: false),
                    used_document_count = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_interactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ai_interactions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ai_interaction_sources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ai_interaction_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    source_type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    source_id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    document_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    document_version_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    wiki_page_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    folder_path = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    excerpt = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    rank = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_interaction_sources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ai_interaction_sources_ai_interactions_ai_interaction_id",
                        column: x => x.ai_interaction_id,
                        principalTable: "ai_interactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_interaction_sources_ai_interaction_id",
                table: "ai_interaction_sources",
                column: "ai_interaction_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_interactions_created_at",
                table: "ai_interactions",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_ai_interactions_user_id",
                table: "ai_interactions",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_interaction_sources");

            migrationBuilder.DropTable(
                name: "ai_interactions");
        }
    }
}
