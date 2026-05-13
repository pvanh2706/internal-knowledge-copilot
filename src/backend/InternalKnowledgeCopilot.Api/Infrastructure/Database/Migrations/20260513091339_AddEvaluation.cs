using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddEvaluation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "evaluation_cases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    source_feedback_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    question = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    expected_answer = table.Column<string>(type: "TEXT", nullable: false),
                    expected_keywords_json = table.Column<string>(type: "TEXT", nullable: true),
                    scope_type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    folder_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    document_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evaluation_cases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_evaluation_cases_ai_feedback_source_feedback_id",
                        column: x => x.source_feedback_id,
                        principalTable: "ai_feedback",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_evaluation_cases_documents_document_id",
                        column: x => x.document_id,
                        principalTable: "documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_evaluation_cases_folders_folder_id",
                        column: x => x.folder_id,
                        principalTable: "folders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_evaluation_cases_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "evaluation_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    total_cases = table.Column<int>(type: "INTEGER", nullable: false),
                    passed_cases = table.Column<int>(type: "INTEGER", nullable: false),
                    failed_cases = table.Column<int>(type: "INTEGER", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    finished_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evaluation_runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_evaluation_runs_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "evaluation_run_results",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    evaluation_run_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    evaluation_case_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ai_interaction_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    actual_answer = table.Column<string>(type: "TEXT", nullable: false),
                    passed = table.Column<bool>(type: "INTEGER", nullable: false),
                    score = table.Column<double>(type: "REAL", nullable: false),
                    failure_reason = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evaluation_run_results", x => x.Id);
                    table.ForeignKey(
                        name: "FK_evaluation_run_results_ai_interactions_ai_interaction_id",
                        column: x => x.ai_interaction_id,
                        principalTable: "ai_interactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_evaluation_run_results_evaluation_cases_evaluation_case_id",
                        column: x => x.evaluation_case_id,
                        principalTable: "evaluation_cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_evaluation_run_results_evaluation_runs_evaluation_run_id",
                        column: x => x.evaluation_run_id,
                        principalTable: "evaluation_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_cases_created_by_user_id",
                table: "evaluation_cases",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_cases_document_id",
                table: "evaluation_cases",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_cases_folder_id",
                table: "evaluation_cases",
                column: "folder_id");

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_cases_is_active_created_at",
                table: "evaluation_cases",
                columns: new[] { "is_active", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_cases_source_feedback_id",
                table: "evaluation_cases",
                column: "source_feedback_id");

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_run_results_ai_interaction_id",
                table: "evaluation_run_results",
                column: "ai_interaction_id");

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_run_results_evaluation_case_id_created_at",
                table: "evaluation_run_results",
                columns: new[] { "evaluation_case_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_run_results_evaluation_run_id",
                table: "evaluation_run_results",
                column: "evaluation_run_id");

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_runs_created_at",
                table: "evaluation_runs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_runs_created_by_user_id",
                table: "evaluation_runs",
                column: "created_by_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "evaluation_run_results");

            migrationBuilder.DropTable(
                name: "evaluation_cases");

            migrationBuilder.DropTable(
                name: "evaluation_runs");
        }
    }
}
