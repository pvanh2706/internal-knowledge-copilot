using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddFeedbackImprovementLoop : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_quality_issues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ai_feedback_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ai_interaction_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    failure_type = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    severity = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    root_cause_hypothesis = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    recommended_actions_json = table.Column<string>(type: "TEXT", nullable: true),
                    evidence_json = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    classified_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_quality_issues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ai_quality_issues_ai_feedback_ai_feedback_id",
                        column: x => x.ai_feedback_id,
                        principalTable: "ai_feedback",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ai_quality_issues_ai_interactions_ai_interaction_id",
                        column: x => x.ai_interaction_id,
                        principalTable: "ai_interactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_corrections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    quality_issue_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ai_feedback_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ai_interaction_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    question = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    correction_text = table.Column<string>(type: "TEXT", nullable: false),
                    visibility_scope = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    folder_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    document_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    reject_reason = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    approved_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    approved_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    indexed_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_corrections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_knowledge_corrections_ai_feedback_ai_feedback_id",
                        column: x => x.ai_feedback_id,
                        principalTable: "ai_feedback",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_knowledge_corrections_ai_interactions_ai_interaction_id",
                        column: x => x.ai_interaction_id,
                        principalTable: "ai_interactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_knowledge_corrections_ai_quality_issues_quality_issue_id",
                        column: x => x.quality_issue_id,
                        principalTable: "ai_quality_issues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_knowledge_corrections_documents_document_id",
                        column: x => x.document_id,
                        principalTable: "documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_knowledge_corrections_folders_folder_id",
                        column: x => x.folder_id,
                        principalTable: "folders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_knowledge_corrections_users_approved_by_user_id",
                        column: x => x.approved_by_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_knowledge_corrections_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "retrieval_hints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    correction_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    hint_text = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_retrieval_hints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_retrieval_hints_knowledge_corrections_correction_id",
                        column: x => x.correction_id,
                        principalTable: "knowledge_corrections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_quality_issues_ai_feedback_id",
                table: "ai_quality_issues",
                column: "ai_feedback_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_quality_issues_ai_interaction_id",
                table: "ai_quality_issues",
                column: "ai_interaction_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_quality_issues_status_created_at",
                table: "ai_quality_issues",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_corrections_ai_feedback_id",
                table: "knowledge_corrections",
                column: "ai_feedback_id");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_corrections_ai_interaction_id",
                table: "knowledge_corrections",
                column: "ai_interaction_id");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_corrections_approved_by_user_id",
                table: "knowledge_corrections",
                column: "approved_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_corrections_created_by_user_id",
                table: "knowledge_corrections",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_corrections_document_id",
                table: "knowledge_corrections",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_corrections_folder_id",
                table: "knowledge_corrections",
                column: "folder_id");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_corrections_quality_issue_id",
                table: "knowledge_corrections",
                column: "quality_issue_id");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_corrections_status_created_at",
                table: "knowledge_corrections",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_retrieval_hints_correction_id",
                table: "retrieval_hints",
                column: "correction_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "retrieval_hints");

            migrationBuilder.DropTable(
                name: "knowledge_corrections");

            migrationBuilder.DropTable(
                name: "ai_quality_issues");
        }
    }
}
