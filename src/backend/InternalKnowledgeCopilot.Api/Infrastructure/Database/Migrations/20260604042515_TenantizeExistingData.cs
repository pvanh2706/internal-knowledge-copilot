using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class TenantizeExistingData : Migration
    {
        private const string DefaultTenantId = "11111111-1111-1111-1111-111111111111";
        private const string DefaultTenantCode = "default";

        private static readonly string[] TenantizedTableNames =
        [
            "wiki_pages",
            "wiki_drafts",
            "users",
            "user_folder_permissions",
            "teams",
            "retrieval_hints",
            "processing_jobs",
            "knowledge_corrections",
            "knowledge_chunks",
            "knowledge_chunk_indexes",
            "folders",
            "folder_permissions",
            "evaluation_runs",
            "evaluation_run_results",
            "evaluation_cases",
            "documents",
            "document_versions",
            "audit_logs",
            "ai_quality_issues",
            "ai_provider_settings",
            "ai_interactions",
            "ai_interaction_sources",
            "ai_feedback",
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                INSERT OR IGNORE INTO tenants (Id, name, code, status, created_at, updated_at, deleted_at)
                VALUES ('{DefaultTenantId}', 'Default Tenant', '{DefaultTenantCode}', 'Active', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, NULL);
                """);

            migrationBuilder.DropIndex(
                name: "IX_wiki_pages_source_draft_id",
                table: "wiki_pages");

            migrationBuilder.DropIndex(
                name: "IX_wiki_pages_visibility_scope",
                table: "wiki_pages");

            migrationBuilder.DropIndex(
                name: "IX_wiki_drafts_status",
                table: "wiki_drafts");

            migrationBuilder.DropIndex(
                name: "IX_users_email",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_user_folder_permissions_user_id_folder_id",
                table: "user_folder_permissions");

            migrationBuilder.DropIndex(
                name: "IX_teams_name",
                table: "teams");

            migrationBuilder.DropIndex(
                name: "IX_processing_jobs_status_created_at",
                table: "processing_jobs");

            migrationBuilder.DropIndex(
                name: "IX_processing_jobs_target_type_target_id",
                table: "processing_jobs");

            migrationBuilder.DropIndex(
                name: "IX_knowledge_corrections_status_created_at",
                table: "knowledge_corrections");

            migrationBuilder.DropIndex(
                name: "IX_knowledge_chunks_source_type_source_id",
                table: "knowledge_chunks");

            migrationBuilder.DropIndex(
                name: "IX_knowledge_chunks_source_type_status",
                table: "knowledge_chunks");

            migrationBuilder.DropIndex(
                name: "IX_knowledge_chunk_indexes_source_type_source_id",
                table: "knowledge_chunk_indexes");

            migrationBuilder.DropIndex(
                name: "IX_knowledge_chunk_indexes_source_type_status",
                table: "knowledge_chunk_indexes");

            migrationBuilder.DropIndex(
                name: "IX_folders_path",
                table: "folders");

            migrationBuilder.DropIndex(
                name: "IX_folder_permissions_folder_id_team_id",
                table: "folder_permissions");

            migrationBuilder.DropIndex(
                name: "IX_evaluation_runs_created_at",
                table: "evaluation_runs");

            migrationBuilder.DropIndex(
                name: "IX_evaluation_run_results_evaluation_case_id_created_at",
                table: "evaluation_run_results");

            migrationBuilder.DropIndex(
                name: "IX_evaluation_cases_is_active_created_at",
                table: "evaluation_cases");

            migrationBuilder.DropIndex(
                name: "IX_documents_folder_id_title",
                table: "documents");

            migrationBuilder.DropIndex(
                name: "IX_document_versions_document_id_version_number",
                table: "document_versions");

            migrationBuilder.DropIndex(
                name: "IX_audit_logs_action",
                table: "audit_logs");

            migrationBuilder.DropIndex(
                name: "IX_audit_logs_created_at",
                table: "audit_logs");

            migrationBuilder.DropIndex(
                name: "IX_audit_logs_entity_type",
                table: "audit_logs");

            migrationBuilder.DropIndex(
                name: "IX_ai_quality_issues_ai_feedback_id",
                table: "ai_quality_issues");

            migrationBuilder.DropIndex(
                name: "IX_ai_quality_issues_status_created_at",
                table: "ai_quality_issues");

            migrationBuilder.DropIndex(
                name: "IX_ai_interactions_created_at",
                table: "ai_interactions");

            migrationBuilder.DropIndex(
                name: "IX_ai_feedback_ai_interaction_id_user_id",
                table: "ai_feedback");

            migrationBuilder.DropIndex(
                name: "IX_ai_feedback_value_review_status",
                table: "ai_feedback");

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "wiki_pages",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "wiki_drafts",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "users",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "user_folder_permissions",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "teams",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "retrieval_hints",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "processing_jobs",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "knowledge_corrections",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "knowledge_chunks",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "knowledge_chunk_indexes",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "folders",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "folder_permissions",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "evaluation_runs",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "evaluation_run_results",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "evaluation_cases",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "documents",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "document_versions",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "audit_logs",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "ai_quality_issues",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "ai_provider_settings",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "ai_interactions",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "ai_interaction_sources",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "ai_feedback",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            foreach (var tableName in TenantizedTableNames)
            {
                migrationBuilder.Sql($"""
                    UPDATE {tableName}
                    SET tenant_id = (SELECT Id FROM tenants WHERE code = '{DefaultTenantCode}' LIMIT 1);
                    """);
            }

            migrationBuilder.CreateIndex(
                name: "IX_wiki_pages_source_draft_id",
                table: "wiki_pages",
                column: "source_draft_id");

            migrationBuilder.CreateIndex(
                name: "IX_wiki_pages_tenant_id",
                table: "wiki_pages",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_wiki_pages_tenant_id_source_draft_id",
                table: "wiki_pages",
                columns: new[] { "tenant_id", "source_draft_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_wiki_pages_tenant_id_visibility_scope",
                table: "wiki_pages",
                columns: new[] { "tenant_id", "visibility_scope" });

            migrationBuilder.CreateIndex(
                name: "IX_wiki_drafts_tenant_id",
                table: "wiki_drafts",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_wiki_drafts_tenant_id_source_document_id",
                table: "wiki_drafts",
                columns: new[] { "tenant_id", "source_document_id" });

            migrationBuilder.CreateIndex(
                name: "IX_wiki_drafts_tenant_id_status",
                table: "wiki_drafts",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_users_tenant_id",
                table: "users",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_users_tenant_id_email",
                table: "users",
                columns: new[] { "tenant_id", "email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_folder_permissions_tenant_id",
                table: "user_folder_permissions",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_folder_permissions_tenant_id_user_id_folder_id",
                table: "user_folder_permissions",
                columns: new[] { "tenant_id", "user_id", "folder_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_folder_permissions_user_id",
                table: "user_folder_permissions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_teams_tenant_id",
                table: "teams",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_teams_tenant_id_name",
                table: "teams",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_retrieval_hints_tenant_id",
                table: "retrieval_hints",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_retrieval_hints_tenant_id_correction_id",
                table: "retrieval_hints",
                columns: new[] { "tenant_id", "correction_id" });

            migrationBuilder.CreateIndex(
                name: "IX_processing_jobs_tenant_id",
                table: "processing_jobs",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_processing_jobs_tenant_id_status_created_at",
                table: "processing_jobs",
                columns: new[] { "tenant_id", "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_processing_jobs_tenant_id_target_type_target_id",
                table: "processing_jobs",
                columns: new[] { "tenant_id", "target_type", "target_id" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_corrections_tenant_id",
                table: "knowledge_corrections",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_corrections_tenant_id_quality_issue_id",
                table: "knowledge_corrections",
                columns: new[] { "tenant_id", "quality_issue_id" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_corrections_tenant_id_status_created_at",
                table: "knowledge_corrections",
                columns: new[] { "tenant_id", "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_chunks_tenant_id",
                table: "knowledge_chunks",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_chunks_tenant_id_source_type_source_id",
                table: "knowledge_chunks",
                columns: new[] { "tenant_id", "source_type", "source_id" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_chunks_tenant_id_source_type_status",
                table: "knowledge_chunks",
                columns: new[] { "tenant_id", "source_type", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_chunk_indexes_tenant_id",
                table: "knowledge_chunk_indexes",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_chunk_indexes_tenant_id_source_type_source_id",
                table: "knowledge_chunk_indexes",
                columns: new[] { "tenant_id", "source_type", "source_id" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_chunk_indexes_tenant_id_source_type_status",
                table: "knowledge_chunk_indexes",
                columns: new[] { "tenant_id", "source_type", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_folders_tenant_id",
                table: "folders",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_folders_tenant_id_path",
                table: "folders",
                columns: new[] { "tenant_id", "path" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_folder_permissions_folder_id",
                table: "folder_permissions",
                column: "folder_id");

            migrationBuilder.CreateIndex(
                name: "IX_folder_permissions_tenant_id",
                table: "folder_permissions",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_folder_permissions_tenant_id_folder_id_team_id",
                table: "folder_permissions",
                columns: new[] { "tenant_id", "folder_id", "team_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_runs_tenant_id",
                table: "evaluation_runs",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_runs_tenant_id_created_at",
                table: "evaluation_runs",
                columns: new[] { "tenant_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_run_results_evaluation_case_id",
                table: "evaluation_run_results",
                column: "evaluation_case_id");

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_run_results_tenant_id",
                table: "evaluation_run_results",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_run_results_tenant_id_evaluation_case_id_created_at",
                table: "evaluation_run_results",
                columns: new[] { "tenant_id", "evaluation_case_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_run_results_tenant_id_evaluation_run_id",
                table: "evaluation_run_results",
                columns: new[] { "tenant_id", "evaluation_run_id" });

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_cases_tenant_id",
                table: "evaluation_cases",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_cases_tenant_id_is_active_created_at",
                table: "evaluation_cases",
                columns: new[] { "tenant_id", "is_active", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_cases_tenant_id_source_feedback_id",
                table: "evaluation_cases",
                columns: new[] { "tenant_id", "source_feedback_id" });

            migrationBuilder.CreateIndex(
                name: "IX_documents_folder_id",
                table: "documents",
                column: "folder_id");

            migrationBuilder.CreateIndex(
                name: "IX_documents_tenant_id",
                table: "documents",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_documents_tenant_id_folder_id_title",
                table: "documents",
                columns: new[] { "tenant_id", "folder_id", "title" });

            migrationBuilder.CreateIndex(
                name: "IX_document_versions_document_id",
                table: "document_versions",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "IX_document_versions_tenant_id",
                table: "document_versions",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_document_versions_tenant_id_document_id_version_number",
                table: "document_versions",
                columns: new[] { "tenant_id", "document_id", "version_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_tenant_id",
                table: "audit_logs",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_tenant_id_action",
                table: "audit_logs",
                columns: new[] { "tenant_id", "action" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_tenant_id_created_at",
                table: "audit_logs",
                columns: new[] { "tenant_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_tenant_id_entity_type",
                table: "audit_logs",
                columns: new[] { "tenant_id", "entity_type" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_quality_issues_ai_feedback_id",
                table: "ai_quality_issues",
                column: "ai_feedback_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_quality_issues_tenant_id",
                table: "ai_quality_issues",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_quality_issues_tenant_id_ai_feedback_id",
                table: "ai_quality_issues",
                columns: new[] { "tenant_id", "ai_feedback_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_quality_issues_tenant_id_status_created_at",
                table: "ai_quality_issues",
                columns: new[] { "tenant_id", "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_provider_settings_tenant_id",
                table: "ai_provider_settings",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_interactions_tenant_id",
                table: "ai_interactions",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_interactions_tenant_id_created_at",
                table: "ai_interactions",
                columns: new[] { "tenant_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_interactions_tenant_id_user_id",
                table: "ai_interactions",
                columns: new[] { "tenant_id", "user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_interaction_sources_tenant_id",
                table: "ai_interaction_sources",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_interaction_sources_tenant_id_ai_interaction_id",
                table: "ai_interaction_sources",
                columns: new[] { "tenant_id", "ai_interaction_id" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_feedback_ai_interaction_id",
                table: "ai_feedback",
                column: "ai_interaction_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_feedback_tenant_id",
                table: "ai_feedback",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_feedback_tenant_id_ai_interaction_id_user_id",
                table: "ai_feedback",
                columns: new[] { "tenant_id", "ai_interaction_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_feedback_tenant_id_value_review_status",
                table: "ai_feedback",
                columns: new[] { "tenant_id", "value", "review_status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_wiki_pages_source_draft_id",
                table: "wiki_pages");

            migrationBuilder.DropIndex(
                name: "IX_wiki_pages_tenant_id",
                table: "wiki_pages");

            migrationBuilder.DropIndex(
                name: "IX_wiki_pages_tenant_id_source_draft_id",
                table: "wiki_pages");

            migrationBuilder.DropIndex(
                name: "IX_wiki_pages_tenant_id_visibility_scope",
                table: "wiki_pages");

            migrationBuilder.DropIndex(
                name: "IX_wiki_drafts_tenant_id",
                table: "wiki_drafts");

            migrationBuilder.DropIndex(
                name: "IX_wiki_drafts_tenant_id_source_document_id",
                table: "wiki_drafts");

            migrationBuilder.DropIndex(
                name: "IX_wiki_drafts_tenant_id_status",
                table: "wiki_drafts");

            migrationBuilder.DropIndex(
                name: "IX_users_tenant_id",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_tenant_id_email",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_user_folder_permissions_tenant_id",
                table: "user_folder_permissions");

            migrationBuilder.DropIndex(
                name: "IX_user_folder_permissions_tenant_id_user_id_folder_id",
                table: "user_folder_permissions");

            migrationBuilder.DropIndex(
                name: "IX_user_folder_permissions_user_id",
                table: "user_folder_permissions");

            migrationBuilder.DropIndex(
                name: "IX_teams_tenant_id",
                table: "teams");

            migrationBuilder.DropIndex(
                name: "IX_teams_tenant_id_name",
                table: "teams");

            migrationBuilder.DropIndex(
                name: "IX_retrieval_hints_tenant_id",
                table: "retrieval_hints");

            migrationBuilder.DropIndex(
                name: "IX_retrieval_hints_tenant_id_correction_id",
                table: "retrieval_hints");

            migrationBuilder.DropIndex(
                name: "IX_processing_jobs_tenant_id",
                table: "processing_jobs");

            migrationBuilder.DropIndex(
                name: "IX_processing_jobs_tenant_id_status_created_at",
                table: "processing_jobs");

            migrationBuilder.DropIndex(
                name: "IX_processing_jobs_tenant_id_target_type_target_id",
                table: "processing_jobs");

            migrationBuilder.DropIndex(
                name: "IX_knowledge_corrections_tenant_id",
                table: "knowledge_corrections");

            migrationBuilder.DropIndex(
                name: "IX_knowledge_corrections_tenant_id_quality_issue_id",
                table: "knowledge_corrections");

            migrationBuilder.DropIndex(
                name: "IX_knowledge_corrections_tenant_id_status_created_at",
                table: "knowledge_corrections");

            migrationBuilder.DropIndex(
                name: "IX_knowledge_chunks_tenant_id",
                table: "knowledge_chunks");

            migrationBuilder.DropIndex(
                name: "IX_knowledge_chunks_tenant_id_source_type_source_id",
                table: "knowledge_chunks");

            migrationBuilder.DropIndex(
                name: "IX_knowledge_chunks_tenant_id_source_type_status",
                table: "knowledge_chunks");

            migrationBuilder.DropIndex(
                name: "IX_knowledge_chunk_indexes_tenant_id",
                table: "knowledge_chunk_indexes");

            migrationBuilder.DropIndex(
                name: "IX_knowledge_chunk_indexes_tenant_id_source_type_source_id",
                table: "knowledge_chunk_indexes");

            migrationBuilder.DropIndex(
                name: "IX_knowledge_chunk_indexes_tenant_id_source_type_status",
                table: "knowledge_chunk_indexes");

            migrationBuilder.DropIndex(
                name: "IX_folders_tenant_id",
                table: "folders");

            migrationBuilder.DropIndex(
                name: "IX_folders_tenant_id_path",
                table: "folders");

            migrationBuilder.DropIndex(
                name: "IX_folder_permissions_folder_id",
                table: "folder_permissions");

            migrationBuilder.DropIndex(
                name: "IX_folder_permissions_tenant_id",
                table: "folder_permissions");

            migrationBuilder.DropIndex(
                name: "IX_folder_permissions_tenant_id_folder_id_team_id",
                table: "folder_permissions");

            migrationBuilder.DropIndex(
                name: "IX_evaluation_runs_tenant_id",
                table: "evaluation_runs");

            migrationBuilder.DropIndex(
                name: "IX_evaluation_runs_tenant_id_created_at",
                table: "evaluation_runs");

            migrationBuilder.DropIndex(
                name: "IX_evaluation_run_results_evaluation_case_id",
                table: "evaluation_run_results");

            migrationBuilder.DropIndex(
                name: "IX_evaluation_run_results_tenant_id",
                table: "evaluation_run_results");

            migrationBuilder.DropIndex(
                name: "IX_evaluation_run_results_tenant_id_evaluation_case_id_created_at",
                table: "evaluation_run_results");

            migrationBuilder.DropIndex(
                name: "IX_evaluation_run_results_tenant_id_evaluation_run_id",
                table: "evaluation_run_results");

            migrationBuilder.DropIndex(
                name: "IX_evaluation_cases_tenant_id",
                table: "evaluation_cases");

            migrationBuilder.DropIndex(
                name: "IX_evaluation_cases_tenant_id_is_active_created_at",
                table: "evaluation_cases");

            migrationBuilder.DropIndex(
                name: "IX_evaluation_cases_tenant_id_source_feedback_id",
                table: "evaluation_cases");

            migrationBuilder.DropIndex(
                name: "IX_documents_folder_id",
                table: "documents");

            migrationBuilder.DropIndex(
                name: "IX_documents_tenant_id",
                table: "documents");

            migrationBuilder.DropIndex(
                name: "IX_documents_tenant_id_folder_id_title",
                table: "documents");

            migrationBuilder.DropIndex(
                name: "IX_document_versions_document_id",
                table: "document_versions");

            migrationBuilder.DropIndex(
                name: "IX_document_versions_tenant_id",
                table: "document_versions");

            migrationBuilder.DropIndex(
                name: "IX_document_versions_tenant_id_document_id_version_number",
                table: "document_versions");

            migrationBuilder.DropIndex(
                name: "IX_audit_logs_tenant_id",
                table: "audit_logs");

            migrationBuilder.DropIndex(
                name: "IX_audit_logs_tenant_id_action",
                table: "audit_logs");

            migrationBuilder.DropIndex(
                name: "IX_audit_logs_tenant_id_created_at",
                table: "audit_logs");

            migrationBuilder.DropIndex(
                name: "IX_audit_logs_tenant_id_entity_type",
                table: "audit_logs");

            migrationBuilder.DropIndex(
                name: "IX_ai_quality_issues_ai_feedback_id",
                table: "ai_quality_issues");

            migrationBuilder.DropIndex(
                name: "IX_ai_quality_issues_tenant_id",
                table: "ai_quality_issues");

            migrationBuilder.DropIndex(
                name: "IX_ai_quality_issues_tenant_id_ai_feedback_id",
                table: "ai_quality_issues");

            migrationBuilder.DropIndex(
                name: "IX_ai_quality_issues_tenant_id_status_created_at",
                table: "ai_quality_issues");

            migrationBuilder.DropIndex(
                name: "IX_ai_provider_settings_tenant_id",
                table: "ai_provider_settings");

            migrationBuilder.DropIndex(
                name: "IX_ai_interactions_tenant_id",
                table: "ai_interactions");

            migrationBuilder.DropIndex(
                name: "IX_ai_interactions_tenant_id_created_at",
                table: "ai_interactions");

            migrationBuilder.DropIndex(
                name: "IX_ai_interactions_tenant_id_user_id",
                table: "ai_interactions");

            migrationBuilder.DropIndex(
                name: "IX_ai_interaction_sources_tenant_id",
                table: "ai_interaction_sources");

            migrationBuilder.DropIndex(
                name: "IX_ai_interaction_sources_tenant_id_ai_interaction_id",
                table: "ai_interaction_sources");

            migrationBuilder.DropIndex(
                name: "IX_ai_feedback_ai_interaction_id",
                table: "ai_feedback");

            migrationBuilder.DropIndex(
                name: "IX_ai_feedback_tenant_id",
                table: "ai_feedback");

            migrationBuilder.DropIndex(
                name: "IX_ai_feedback_tenant_id_ai_interaction_id_user_id",
                table: "ai_feedback");

            migrationBuilder.DropIndex(
                name: "IX_ai_feedback_tenant_id_value_review_status",
                table: "ai_feedback");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "wiki_pages");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "wiki_drafts");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "user_folder_permissions");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "teams");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "retrieval_hints");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "processing_jobs");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "knowledge_corrections");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "knowledge_chunks");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "knowledge_chunk_indexes");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "folders");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "folder_permissions");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "evaluation_runs");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "evaluation_run_results");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "evaluation_cases");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "document_versions");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "ai_quality_issues");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "ai_provider_settings");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "ai_interactions");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "ai_interaction_sources");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "ai_feedback");

            migrationBuilder.CreateIndex(
                name: "IX_wiki_pages_source_draft_id",
                table: "wiki_pages",
                column: "source_draft_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_wiki_pages_visibility_scope",
                table: "wiki_pages",
                column: "visibility_scope");

            migrationBuilder.CreateIndex(
                name: "IX_wiki_drafts_status",
                table: "wiki_drafts",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_folder_permissions_user_id_folder_id",
                table: "user_folder_permissions",
                columns: new[] { "user_id", "folder_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_teams_name",
                table: "teams",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_processing_jobs_status_created_at",
                table: "processing_jobs",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_processing_jobs_target_type_target_id",
                table: "processing_jobs",
                columns: new[] { "target_type", "target_id" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_corrections_status_created_at",
                table: "knowledge_corrections",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_chunks_source_type_source_id",
                table: "knowledge_chunks",
                columns: new[] { "source_type", "source_id" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_chunks_source_type_status",
                table: "knowledge_chunks",
                columns: new[] { "source_type", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_chunk_indexes_source_type_source_id",
                table: "knowledge_chunk_indexes",
                columns: new[] { "source_type", "source_id" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_chunk_indexes_source_type_status",
                table: "knowledge_chunk_indexes",
                columns: new[] { "source_type", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_folders_path",
                table: "folders",
                column: "path",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_folder_permissions_folder_id_team_id",
                table: "folder_permissions",
                columns: new[] { "folder_id", "team_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_runs_created_at",
                table: "evaluation_runs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_run_results_evaluation_case_id_created_at",
                table: "evaluation_run_results",
                columns: new[] { "evaluation_case_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_cases_is_active_created_at",
                table: "evaluation_cases",
                columns: new[] { "is_active", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_documents_folder_id_title",
                table: "documents",
                columns: new[] { "folder_id", "title" });

            migrationBuilder.CreateIndex(
                name: "IX_document_versions_document_id_version_number",
                table: "document_versions",
                columns: new[] { "document_id", "version_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_action",
                table: "audit_logs",
                column: "action");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_created_at",
                table: "audit_logs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_entity_type",
                table: "audit_logs",
                column: "entity_type");

            migrationBuilder.CreateIndex(
                name: "IX_ai_quality_issues_ai_feedback_id",
                table: "ai_quality_issues",
                column: "ai_feedback_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_quality_issues_status_created_at",
                table: "ai_quality_issues",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_interactions_created_at",
                table: "ai_interactions",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_ai_feedback_ai_interaction_id_user_id",
                table: "ai_feedback",
                columns: new[] { "ai_interaction_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_feedback_value_review_status",
                table: "ai_feedback",
                columns: new[] { "value", "review_status" });
        }
    }
}
