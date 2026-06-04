using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeSourcesAndExternalObjects : Migration
    {
        private const string DefaultTenantCode = "default";
        private const string DefaultApplicationId = "33333333-3333-3333-3333-333333333333";
        private const string DefaultApplicationCode = "internal-knowledge-copilot";
        private const string DefaultLocalKnowledgeSourceId = "22222222-2222-2222-2222-222222222222";
        private const string DefaultLocalKnowledgeSourceExternalId = "local";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "knowledge_source_id",
                table: "wiki_pages",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "knowledge_source_id",
                table: "documents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "knowledge_sources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    application_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    source_type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    external_source_id = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    sync_mode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    metadata_json = table.Column<string>(type: "TEXT", nullable: true),
                    last_sync_started_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    last_sync_completed_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    last_sync_status = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    last_sync_error = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_sources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_knowledge_sources_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_knowledge_sources_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql($"""
                INSERT OR IGNORE INTO applications
                    (Id, tenant_id, code, name, application_type, base_url, status, created_at, updated_at, deleted_at)
                SELECT
                    '{DefaultApplicationId}',
                    tenants.Id,
                    '{DefaultApplicationCode}',
                    'Internal Knowledge Copilot',
                    'Internal',
                    NULL,
                    'Active',
                    CURRENT_TIMESTAMP,
                    CURRENT_TIMESTAMP,
                    NULL
                FROM tenants
                WHERE tenants.code = '{DefaultTenantCode}'
                  AND NOT EXISTS (
                      SELECT 1
                      FROM applications
                      WHERE applications.tenant_id = tenants.Id
                        AND applications.code = '{DefaultApplicationCode}'
                  );
                """);

            migrationBuilder.Sql($"""
                INSERT OR IGNORE INTO knowledge_sources
                    (Id, tenant_id, application_id, source_type, external_source_id, name, sync_mode, status, metadata_json, last_sync_started_at, last_sync_completed_at, last_sync_status, last_sync_error, created_at, updated_at, deleted_at)
                SELECT
                    '{DefaultLocalKnowledgeSourceId}',
                    applications.tenant_id,
                    applications.Id,
                    'Local',
                    '{DefaultLocalKnowledgeSourceExternalId}',
                    'Local uploads and wiki',
                    'Manual',
                    'Active',
                    NULL,
                    NULL,
                    NULL,
                    NULL,
                    NULL,
                    CURRENT_TIMESTAMP,
                    CURRENT_TIMESTAMP,
                    NULL
                FROM applications
                JOIN tenants ON tenants.Id = applications.tenant_id
                WHERE tenants.code = '{DefaultTenantCode}'
                  AND applications.code = '{DefaultApplicationCode}'
                  AND NOT EXISTS (
                      SELECT 1
                      FROM knowledge_sources
                      WHERE knowledge_sources.tenant_id = applications.tenant_id
                        AND knowledge_sources.application_id = applications.Id
                        AND knowledge_sources.source_type = 'Local'
                        AND knowledge_sources.external_source_id = '{DefaultLocalKnowledgeSourceExternalId}'
                  );
                """);

            migrationBuilder.Sql($"""
                UPDATE documents
                SET knowledge_source_id = (
                    SELECT knowledge_sources.Id
                    FROM knowledge_sources
                    JOIN applications ON applications.Id = knowledge_sources.application_id
                    WHERE knowledge_sources.tenant_id = documents.tenant_id
                      AND applications.code = '{DefaultApplicationCode}'
                      AND knowledge_sources.source_type = 'Local'
                      AND knowledge_sources.external_source_id = '{DefaultLocalKnowledgeSourceExternalId}'
                    LIMIT 1
                )
                WHERE knowledge_source_id IS NULL;
                """);

            migrationBuilder.Sql($"""
                UPDATE wiki_pages
                SET knowledge_source_id = (
                    SELECT knowledge_sources.Id
                    FROM knowledge_sources
                    JOIN applications ON applications.Id = knowledge_sources.application_id
                    WHERE knowledge_sources.tenant_id = wiki_pages.tenant_id
                      AND applications.code = '{DefaultApplicationCode}'
                      AND knowledge_sources.source_type = 'Local'
                      AND knowledge_sources.external_source_id = '{DefaultLocalKnowledgeSourceExternalId}'
                    LIMIT 1
                )
                WHERE knowledge_source_id IS NULL;
                """);

            migrationBuilder.CreateTable(
                name: "external_objects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    application_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    knowledge_source_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    object_type = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    external_object_id = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    url = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    metadata_json = table.Column<string>(type: "TEXT", nullable: true),
                    content_hash = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    acl_hash = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    last_synced_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    content_synced_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    acl_synced_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_external_objects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_external_objects_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_external_objects_knowledge_sources_knowledge_source_id",
                        column: x => x.knowledge_source_id,
                        principalTable: "knowledge_sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_external_objects_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "external_acl_snapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    application_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    external_object_record_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    object_type = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    external_object_id = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    subject_type = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    subject_id = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    subject_display_name = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    permission = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    valid_from = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    valid_to = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    metadata_json = table.Column<string>(type: "TEXT", nullable: true),
                    synced_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_external_acl_snapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_external_acl_snapshots_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_external_acl_snapshots_external_objects_external_object_record_id",
                        column: x => x.external_object_record_id,
                        principalTable: "external_objects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_external_acl_snapshots_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_wiki_pages_knowledge_source_id",
                table: "wiki_pages",
                column: "knowledge_source_id");

            migrationBuilder.CreateIndex(
                name: "IX_wiki_pages_tenant_id_knowledge_source_id",
                table: "wiki_pages",
                columns: new[] { "tenant_id", "knowledge_source_id" });

            migrationBuilder.CreateIndex(
                name: "IX_documents_knowledge_source_id",
                table: "documents",
                column: "knowledge_source_id");

            migrationBuilder.CreateIndex(
                name: "IX_documents_tenant_id_knowledge_source_id",
                table: "documents",
                columns: new[] { "tenant_id", "knowledge_source_id" });

            migrationBuilder.CreateIndex(
                name: "IX_external_acl_snapshots_application_id",
                table: "external_acl_snapshots",
                column: "application_id");

            migrationBuilder.CreateIndex(
                name: "IX_external_acl_snapshots_external_object_record_id",
                table: "external_acl_snapshots",
                column: "external_object_record_id");

            migrationBuilder.CreateIndex(
                name: "IX_external_acl_snapshots_tenant_id",
                table: "external_acl_snapshots",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_external_acl_snapshots_tenant_id_application_id_object_type_external_object_id",
                table: "external_acl_snapshots",
                columns: new[] { "tenant_id", "application_id", "object_type", "external_object_id" });

            migrationBuilder.CreateIndex(
                name: "IX_external_acl_snapshots_tenant_id_application_id_object_type_external_object_id_subject_type_subject_id_permission",
                table: "external_acl_snapshots",
                columns: new[] { "tenant_id", "application_id", "object_type", "external_object_id", "subject_type", "subject_id", "permission" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_external_objects_application_id",
                table: "external_objects",
                column: "application_id");

            migrationBuilder.CreateIndex(
                name: "IX_external_objects_knowledge_source_id",
                table: "external_objects",
                column: "knowledge_source_id");

            migrationBuilder.CreateIndex(
                name: "IX_external_objects_tenant_id",
                table: "external_objects",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_external_objects_tenant_id_application_id_object_type_external_object_id",
                table: "external_objects",
                columns: new[] { "tenant_id", "application_id", "object_type", "external_object_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_external_objects_tenant_id_status",
                table: "external_objects",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_sources_application_id",
                table: "knowledge_sources",
                column: "application_id");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_sources_tenant_id",
                table: "knowledge_sources",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_sources_tenant_id_application_id_source_type_external_source_id",
                table: "knowledge_sources",
                columns: new[] { "tenant_id", "application_id", "source_type", "external_source_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_sources_tenant_id_status",
                table: "knowledge_sources",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.AddForeignKey(
                name: "FK_documents_knowledge_sources_knowledge_source_id",
                table: "documents",
                column: "knowledge_source_id",
                principalTable: "knowledge_sources",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_wiki_pages_knowledge_sources_knowledge_source_id",
                table: "wiki_pages",
                column: "knowledge_source_id",
                principalTable: "knowledge_sources",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_documents_knowledge_sources_knowledge_source_id",
                table: "documents");

            migrationBuilder.DropForeignKey(
                name: "FK_wiki_pages_knowledge_sources_knowledge_source_id",
                table: "wiki_pages");

            migrationBuilder.DropTable(
                name: "external_acl_snapshots");

            migrationBuilder.DropTable(
                name: "external_objects");

            migrationBuilder.DropTable(
                name: "knowledge_sources");

            migrationBuilder.DropIndex(
                name: "IX_wiki_pages_knowledge_source_id",
                table: "wiki_pages");

            migrationBuilder.DropIndex(
                name: "IX_wiki_pages_tenant_id_knowledge_source_id",
                table: "wiki_pages");

            migrationBuilder.DropIndex(
                name: "IX_documents_knowledge_source_id",
                table: "documents");

            migrationBuilder.DropIndex(
                name: "IX_documents_tenant_id_knowledge_source_id",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "knowledge_source_id",
                table: "wiki_pages");

            migrationBuilder.DropColumn(
                name: "knowledge_source_id",
                table: "documents");
        }
    }
}
