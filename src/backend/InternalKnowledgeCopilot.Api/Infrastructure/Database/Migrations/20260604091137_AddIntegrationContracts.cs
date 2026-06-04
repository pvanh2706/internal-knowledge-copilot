using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddIntegrationContracts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "integration_connections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    application_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    base_url = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    auth_mode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    secret_reference = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    secret_hash = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    secret_rotated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    metadata_json = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_integration_connections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_integration_connections_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_integration_connections_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "integration_inbound_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    application_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    integration_connection_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    event_type = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    idempotency_key = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    external_event_id = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    object_type = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    external_object_id = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    payload_json = table.Column<string>(type: "TEXT", nullable: true),
                    status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_integration_inbound_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_integration_inbound_events_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_integration_inbound_events_integration_connections_integration_connection_id",
                        column: x => x.integration_connection_id,
                        principalTable: "integration_connections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_integration_inbound_events_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_integration_connections_application_id",
                table: "integration_connections",
                column: "application_id");

            migrationBuilder.CreateIndex(
                name: "IX_integration_connections_tenant_id",
                table: "integration_connections",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_integration_connections_tenant_id_application_id_secret_reference",
                table: "integration_connections",
                columns: new[] { "tenant_id", "application_id", "secret_reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_integration_connections_tenant_id_status",
                table: "integration_connections",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_integration_inbound_events_application_id",
                table: "integration_inbound_events",
                column: "application_id");

            migrationBuilder.CreateIndex(
                name: "IX_integration_inbound_events_integration_connection_id",
                table: "integration_inbound_events",
                column: "integration_connection_id");

            migrationBuilder.CreateIndex(
                name: "IX_integration_inbound_events_tenant_id",
                table: "integration_inbound_events",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_integration_inbound_events_tenant_id_application_id_event_type_received_at",
                table: "integration_inbound_events",
                columns: new[] { "tenant_id", "application_id", "event_type", "received_at" });

            migrationBuilder.CreateIndex(
                name: "IX_integration_inbound_events_tenant_id_application_id_idempotency_key",
                table: "integration_inbound_events",
                columns: new[] { "tenant_id", "application_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_integration_inbound_events_tenant_id_application_id_object_type_external_object_id",
                table: "integration_inbound_events",
                columns: new[] { "tenant_id", "application_id", "object_type", "external_object_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "integration_inbound_events");

            migrationBuilder.DropTable(
                name: "integration_connections");
        }
    }
}
