using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddRetrievalSourceMetadata : Migration
    {
        private const string DefaultApplicationCode = "internal-knowledge-copilot";
        private const string DefaultLocalKnowledgeSourceExternalId = "local";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "application_id",
                table: "knowledge_chunks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "external_object_id",
                table: "knowledge_chunks",
                type: "TEXT",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "external_object_record_id",
                table: "knowledge_chunks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "external_object_type",
                table: "knowledge_chunks",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "knowledge_source_id",
                table: "knowledge_chunks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "application_id",
                table: "knowledge_chunk_indexes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "external_object_id",
                table: "knowledge_chunk_indexes",
                type: "TEXT",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "external_object_record_id",
                table: "knowledge_chunk_indexes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "external_object_type",
                table: "knowledge_chunk_indexes",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "knowledge_source_id",
                table: "knowledge_chunk_indexes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "application_id",
                table: "ai_interaction_sources",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "external_object_id",
                table: "ai_interaction_sources",
                type: "TEXT",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "external_object_record_id",
                table: "ai_interaction_sources",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "external_object_type",
                table: "ai_interaction_sources",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "knowledge_source_id",
                table: "ai_interaction_sources",
                type: "TEXT",
                nullable: true);

            BackfillLocalKnowledgeSourceMetadata(migrationBuilder, "knowledge_chunks");
            BackfillLocalKnowledgeSourceMetadata(migrationBuilder, "knowledge_chunk_indexes");
            BackfillLocalKnowledgeSourceMetadata(migrationBuilder, "ai_interaction_sources");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_chunks_tenant_id_application_id",
                table: "knowledge_chunks",
                columns: new[] { "tenant_id", "application_id" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_chunks_tenant_id_application_id_external_object_type_external_object_id",
                table: "knowledge_chunks",
                columns: new[] { "tenant_id", "application_id", "external_object_type", "external_object_id" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_chunks_tenant_id_application_id_knowledge_source_id",
                table: "knowledge_chunks",
                columns: new[] { "tenant_id", "application_id", "knowledge_source_id" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_chunks_tenant_id_knowledge_source_id",
                table: "knowledge_chunks",
                columns: new[] { "tenant_id", "knowledge_source_id" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_chunk_indexes_tenant_id_application_id",
                table: "knowledge_chunk_indexes",
                columns: new[] { "tenant_id", "application_id" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_chunk_indexes_tenant_id_application_id_external_object_type_external_object_id",
                table: "knowledge_chunk_indexes",
                columns: new[] { "tenant_id", "application_id", "external_object_type", "external_object_id" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_chunk_indexes_tenant_id_application_id_knowledge_source_id",
                table: "knowledge_chunk_indexes",
                columns: new[] { "tenant_id", "application_id", "knowledge_source_id" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_chunk_indexes_tenant_id_knowledge_source_id",
                table: "knowledge_chunk_indexes",
                columns: new[] { "tenant_id", "knowledge_source_id" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_interaction_sources_tenant_id_application_id",
                table: "ai_interaction_sources",
                columns: new[] { "tenant_id", "application_id" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_interaction_sources_tenant_id_application_id_external_object_type_external_object_id",
                table: "ai_interaction_sources",
                columns: new[] { "tenant_id", "application_id", "external_object_type", "external_object_id" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_interaction_sources_tenant_id_knowledge_source_id",
                table: "ai_interaction_sources",
                columns: new[] { "tenant_id", "knowledge_source_id" });
        }

        private static void BackfillLocalKnowledgeSourceMetadata(MigrationBuilder migrationBuilder, string tableName)
        {
            migrationBuilder.Sql($"""
                UPDATE {tableName}
                SET knowledge_source_id = (
                    SELECT wiki_pages.knowledge_source_id
                    FROM wiki_pages
                    WHERE wiki_pages.tenant_id = {tableName}.tenant_id
                      AND wiki_pages.Id = {tableName}.wiki_page_id
                    LIMIT 1
                )
                WHERE knowledge_source_id IS NULL
                  AND wiki_page_id IS NOT NULL;
                """);

            migrationBuilder.Sql($"""
                UPDATE {tableName}
                SET knowledge_source_id = (
                    SELECT documents.knowledge_source_id
                    FROM documents
                    WHERE documents.tenant_id = {tableName}.tenant_id
                      AND documents.Id = {tableName}.document_id
                    LIMIT 1
                )
                WHERE knowledge_source_id IS NULL
                  AND document_id IS NOT NULL;
                """);

            migrationBuilder.Sql($"""
                UPDATE {tableName}
                SET knowledge_source_id = (
                    SELECT knowledge_sources.Id
                    FROM knowledge_sources
                    JOIN applications ON applications.Id = knowledge_sources.application_id
                    WHERE knowledge_sources.tenant_id = {tableName}.tenant_id
                      AND applications.code = '{DefaultApplicationCode}'
                      AND knowledge_sources.source_type = 'Local'
                      AND knowledge_sources.external_source_id = '{DefaultLocalKnowledgeSourceExternalId}'
                    LIMIT 1
                )
                WHERE knowledge_source_id IS NULL
                  AND source_type = 'Correction';
                """);

            migrationBuilder.Sql($"""
                UPDATE {tableName}
                SET application_id = (
                    SELECT knowledge_sources.application_id
                    FROM knowledge_sources
                    WHERE knowledge_sources.Id = {tableName}.knowledge_source_id
                      AND knowledge_sources.tenant_id = {tableName}.tenant_id
                    LIMIT 1
                )
                WHERE application_id IS NULL
                  AND knowledge_source_id IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_knowledge_chunks_tenant_id_application_id",
                table: "knowledge_chunks");

            migrationBuilder.DropIndex(
                name: "IX_knowledge_chunks_tenant_id_application_id_external_object_type_external_object_id",
                table: "knowledge_chunks");

            migrationBuilder.DropIndex(
                name: "IX_knowledge_chunks_tenant_id_application_id_knowledge_source_id",
                table: "knowledge_chunks");

            migrationBuilder.DropIndex(
                name: "IX_knowledge_chunks_tenant_id_knowledge_source_id",
                table: "knowledge_chunks");

            migrationBuilder.DropIndex(
                name: "IX_knowledge_chunk_indexes_tenant_id_application_id",
                table: "knowledge_chunk_indexes");

            migrationBuilder.DropIndex(
                name: "IX_knowledge_chunk_indexes_tenant_id_application_id_external_object_type_external_object_id",
                table: "knowledge_chunk_indexes");

            migrationBuilder.DropIndex(
                name: "IX_knowledge_chunk_indexes_tenant_id_application_id_knowledge_source_id",
                table: "knowledge_chunk_indexes");

            migrationBuilder.DropIndex(
                name: "IX_knowledge_chunk_indexes_tenant_id_knowledge_source_id",
                table: "knowledge_chunk_indexes");

            migrationBuilder.DropIndex(
                name: "IX_ai_interaction_sources_tenant_id_application_id",
                table: "ai_interaction_sources");

            migrationBuilder.DropIndex(
                name: "IX_ai_interaction_sources_tenant_id_application_id_external_object_type_external_object_id",
                table: "ai_interaction_sources");

            migrationBuilder.DropIndex(
                name: "IX_ai_interaction_sources_tenant_id_knowledge_source_id",
                table: "ai_interaction_sources");

            migrationBuilder.DropColumn(
                name: "application_id",
                table: "knowledge_chunks");

            migrationBuilder.DropColumn(
                name: "external_object_id",
                table: "knowledge_chunks");

            migrationBuilder.DropColumn(
                name: "external_object_record_id",
                table: "knowledge_chunks");

            migrationBuilder.DropColumn(
                name: "external_object_type",
                table: "knowledge_chunks");

            migrationBuilder.DropColumn(
                name: "knowledge_source_id",
                table: "knowledge_chunks");

            migrationBuilder.DropColumn(
                name: "application_id",
                table: "knowledge_chunk_indexes");

            migrationBuilder.DropColumn(
                name: "external_object_id",
                table: "knowledge_chunk_indexes");

            migrationBuilder.DropColumn(
                name: "external_object_record_id",
                table: "knowledge_chunk_indexes");

            migrationBuilder.DropColumn(
                name: "external_object_type",
                table: "knowledge_chunk_indexes");

            migrationBuilder.DropColumn(
                name: "knowledge_source_id",
                table: "knowledge_chunk_indexes");

            migrationBuilder.DropColumn(
                name: "application_id",
                table: "ai_interaction_sources");

            migrationBuilder.DropColumn(
                name: "external_object_id",
                table: "ai_interaction_sources");

            migrationBuilder.DropColumn(
                name: "external_object_record_id",
                table: "ai_interaction_sources");

            migrationBuilder.DropColumn(
                name: "external_object_type",
                table: "ai_interaction_sources");

            migrationBuilder.DropColumn(
                name: "knowledge_source_id",
                table: "ai_interaction_sources");
        }
    }
}
