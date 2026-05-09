using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddWikiDraftsAndPages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "wiki_drafts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    source_document_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    source_document_version_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    content = table.Column<string>(type: "TEXT", nullable: false),
                    language = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    reject_reason = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    generated_by_user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    reviewed_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    reviewed_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wiki_drafts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_wiki_drafts_document_versions_source_document_version_id",
                        column: x => x.source_document_version_id,
                        principalTable: "document_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_wiki_drafts_documents_source_document_id",
                        column: x => x.source_document_id,
                        principalTable: "documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_wiki_drafts_users_generated_by_user_id",
                        column: x => x.generated_by_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_wiki_drafts_users_reviewed_by_user_id",
                        column: x => x.reviewed_by_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "wiki_pages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    source_draft_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    source_document_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    source_document_version_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    content = table.Column<string>(type: "TEXT", nullable: false),
                    language = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    visibility_scope = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    folder_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    is_company_public_confirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    published_by_user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    archived_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wiki_pages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_wiki_pages_document_versions_source_document_version_id",
                        column: x => x.source_document_version_id,
                        principalTable: "document_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_wiki_pages_documents_source_document_id",
                        column: x => x.source_document_id,
                        principalTable: "documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_wiki_pages_folders_folder_id",
                        column: x => x.folder_id,
                        principalTable: "folders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_wiki_pages_users_published_by_user_id",
                        column: x => x.published_by_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_wiki_pages_wiki_drafts_source_draft_id",
                        column: x => x.source_draft_id,
                        principalTable: "wiki_drafts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_wiki_drafts_generated_by_user_id",
                table: "wiki_drafts",
                column: "generated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_wiki_drafts_reviewed_by_user_id",
                table: "wiki_drafts",
                column: "reviewed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_wiki_drafts_source_document_id",
                table: "wiki_drafts",
                column: "source_document_id");

            migrationBuilder.CreateIndex(
                name: "IX_wiki_drafts_source_document_version_id",
                table: "wiki_drafts",
                column: "source_document_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_wiki_drafts_status",
                table: "wiki_drafts",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_wiki_pages_folder_id",
                table: "wiki_pages",
                column: "folder_id");

            migrationBuilder.CreateIndex(
                name: "IX_wiki_pages_published_by_user_id",
                table: "wiki_pages",
                column: "published_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_wiki_pages_source_document_id",
                table: "wiki_pages",
                column: "source_document_id");

            migrationBuilder.CreateIndex(
                name: "IX_wiki_pages_source_document_version_id",
                table: "wiki_pages",
                column: "source_document_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_wiki_pages_source_draft_id",
                table: "wiki_pages",
                column: "source_draft_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_wiki_pages_visibility_scope",
                table: "wiki_pages",
                column: "visibility_scope");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "wiki_pages");

            migrationBuilder.DropTable(
                name: "wiki_drafts");
        }
    }
}
