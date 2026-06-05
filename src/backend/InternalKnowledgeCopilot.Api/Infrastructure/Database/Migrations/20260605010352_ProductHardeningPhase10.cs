using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class ProductHardeningPhase10 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ai_metadata_json",
                table: "evaluation_runs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "cross_tenant_leakage_cases",
                table: "evaluation_runs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "cross_tenant_leakage_failures",
                table: "evaluation_runs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "application_id",
                table: "evaluation_cases",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "case_kind",
                table: "evaluation_cases",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "Regression");

            migrationBuilder.AddColumn<string>(
                name: "external_object_id",
                table: "evaluation_cases",
                type: "TEXT",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "external_object_type",
                table: "evaluation_cases",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "forbidden_keywords_json",
                table: "evaluation_cases",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "knowledge_source_id",
                table: "evaluation_cases",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ai_model",
                table: "ai_recommendations",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ai_provider_name",
                table: "ai_recommendations",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ai_request_metadata_json",
                table: "ai_recommendations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ai_task_type",
                table: "ai_recommendations",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "embedding_model",
                table: "ai_recommendations",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "embedding_provider_name",
                table: "ai_recommendations",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "latency_ms",
                table: "ai_recommendations",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "prompt_hash",
                table: "ai_recommendations",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "prompt_template_id",
                table: "ai_recommendations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "prompt_template_version",
                table: "ai_recommendations",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "retrieval_metadata_json",
                table: "ai_recommendations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "retrieval_pipeline",
                table: "ai_recommendations",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ai_model",
                table: "ai_interactions",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ai_provider_name",
                table: "ai_interactions",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ai_request_metadata_json",
                table: "ai_interactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ai_task_type",
                table: "ai_interactions",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "embedding_model",
                table: "ai_interactions",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "embedding_provider_name",
                table: "ai_interactions",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "prompt_hash",
                table: "ai_interactions",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "prompt_template_id",
                table: "ai_interactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "prompt_template_version",
                table: "ai_interactions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "retrieval_metadata_json",
                table: "ai_interactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "retrieval_pipeline",
                table: "ai_interactions",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ai_prompt_templates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    task_type = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    version = table.Column<int>(type: "INTEGER", nullable: false),
                    system_prompt = table.Column<string>(type: "TEXT", nullable: false),
                    user_prompt_template = table.Column<string>(type: "TEXT", nullable: false),
                    prompt_hash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    is_default = table.Column<bool>(type: "INTEGER", nullable: false),
                    metadata_json = table.Column<string>(type: "TEXT", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_prompt_templates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ai_prompt_templates_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_prompt_templates_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_cases_application_id",
                table: "evaluation_cases",
                column: "application_id");

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_cases_knowledge_source_id",
                table: "evaluation_cases",
                column: "knowledge_source_id");

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_cases_tenant_id_case_kind_is_active",
                table: "evaluation_cases",
                columns: new[] { "tenant_id", "case_kind", "is_active" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_recommendations_prompt_template_id",
                table: "ai_recommendations",
                column: "prompt_template_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_interactions_prompt_template_id",
                table: "ai_interactions",
                column: "prompt_template_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_prompt_templates_created_by_user_id",
                table: "ai_prompt_templates",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_prompt_templates_tenant_id",
                table: "ai_prompt_templates",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_prompt_templates_tenant_id_task_type_status_is_default",
                table: "ai_prompt_templates",
                columns: new[] { "tenant_id", "task_type", "status", "is_default" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_prompt_templates_tenant_id_task_type_version",
                table: "ai_prompt_templates",
                columns: new[] { "tenant_id", "task_type", "version" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ai_interactions_ai_prompt_templates_prompt_template_id",
                table: "ai_interactions",
                column: "prompt_template_id",
                principalTable: "ai_prompt_templates",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ai_recommendations_ai_prompt_templates_prompt_template_id",
                table: "ai_recommendations",
                column: "prompt_template_id",
                principalTable: "ai_prompt_templates",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_evaluation_cases_applications_application_id",
                table: "evaluation_cases",
                column: "application_id",
                principalTable: "applications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_evaluation_cases_knowledge_sources_knowledge_source_id",
                table: "evaluation_cases",
                column: "knowledge_source_id",
                principalTable: "knowledge_sources",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ai_interactions_ai_prompt_templates_prompt_template_id",
                table: "ai_interactions");

            migrationBuilder.DropForeignKey(
                name: "FK_ai_recommendations_ai_prompt_templates_prompt_template_id",
                table: "ai_recommendations");

            migrationBuilder.DropForeignKey(
                name: "FK_evaluation_cases_applications_application_id",
                table: "evaluation_cases");

            migrationBuilder.DropForeignKey(
                name: "FK_evaluation_cases_knowledge_sources_knowledge_source_id",
                table: "evaluation_cases");

            migrationBuilder.DropTable(
                name: "ai_prompt_templates");

            migrationBuilder.DropIndex(
                name: "IX_evaluation_cases_application_id",
                table: "evaluation_cases");

            migrationBuilder.DropIndex(
                name: "IX_evaluation_cases_knowledge_source_id",
                table: "evaluation_cases");

            migrationBuilder.DropIndex(
                name: "IX_evaluation_cases_tenant_id_case_kind_is_active",
                table: "evaluation_cases");

            migrationBuilder.DropIndex(
                name: "IX_ai_recommendations_prompt_template_id",
                table: "ai_recommendations");

            migrationBuilder.DropIndex(
                name: "IX_ai_interactions_prompt_template_id",
                table: "ai_interactions");

            migrationBuilder.DropColumn(
                name: "ai_metadata_json",
                table: "evaluation_runs");

            migrationBuilder.DropColumn(
                name: "cross_tenant_leakage_cases",
                table: "evaluation_runs");

            migrationBuilder.DropColumn(
                name: "cross_tenant_leakage_failures",
                table: "evaluation_runs");

            migrationBuilder.DropColumn(
                name: "application_id",
                table: "evaluation_cases");

            migrationBuilder.DropColumn(
                name: "case_kind",
                table: "evaluation_cases");

            migrationBuilder.DropColumn(
                name: "external_object_id",
                table: "evaluation_cases");

            migrationBuilder.DropColumn(
                name: "external_object_type",
                table: "evaluation_cases");

            migrationBuilder.DropColumn(
                name: "forbidden_keywords_json",
                table: "evaluation_cases");

            migrationBuilder.DropColumn(
                name: "knowledge_source_id",
                table: "evaluation_cases");

            migrationBuilder.DropColumn(
                name: "ai_model",
                table: "ai_recommendations");

            migrationBuilder.DropColumn(
                name: "ai_provider_name",
                table: "ai_recommendations");

            migrationBuilder.DropColumn(
                name: "ai_request_metadata_json",
                table: "ai_recommendations");

            migrationBuilder.DropColumn(
                name: "ai_task_type",
                table: "ai_recommendations");

            migrationBuilder.DropColumn(
                name: "embedding_model",
                table: "ai_recommendations");

            migrationBuilder.DropColumn(
                name: "embedding_provider_name",
                table: "ai_recommendations");

            migrationBuilder.DropColumn(
                name: "latency_ms",
                table: "ai_recommendations");

            migrationBuilder.DropColumn(
                name: "prompt_hash",
                table: "ai_recommendations");

            migrationBuilder.DropColumn(
                name: "prompt_template_id",
                table: "ai_recommendations");

            migrationBuilder.DropColumn(
                name: "prompt_template_version",
                table: "ai_recommendations");

            migrationBuilder.DropColumn(
                name: "retrieval_metadata_json",
                table: "ai_recommendations");

            migrationBuilder.DropColumn(
                name: "retrieval_pipeline",
                table: "ai_recommendations");

            migrationBuilder.DropColumn(
                name: "ai_model",
                table: "ai_interactions");

            migrationBuilder.DropColumn(
                name: "ai_provider_name",
                table: "ai_interactions");

            migrationBuilder.DropColumn(
                name: "ai_request_metadata_json",
                table: "ai_interactions");

            migrationBuilder.DropColumn(
                name: "ai_task_type",
                table: "ai_interactions");

            migrationBuilder.DropColumn(
                name: "embedding_model",
                table: "ai_interactions");

            migrationBuilder.DropColumn(
                name: "embedding_provider_name",
                table: "ai_interactions");

            migrationBuilder.DropColumn(
                name: "prompt_hash",
                table: "ai_interactions");

            migrationBuilder.DropColumn(
                name: "prompt_template_id",
                table: "ai_interactions");

            migrationBuilder.DropColumn(
                name: "prompt_template_version",
                table: "ai_interactions");

            migrationBuilder.DropColumn(
                name: "retrieval_metadata_json",
                table: "ai_interactions");

            migrationBuilder.DropColumn(
                name: "retrieval_pipeline",
                table: "ai_interactions");
        }
    }
}
