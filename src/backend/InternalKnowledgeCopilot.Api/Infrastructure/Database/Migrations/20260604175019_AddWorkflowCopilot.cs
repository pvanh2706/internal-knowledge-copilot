using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowCopilot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "workflow_definitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    application_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    event_type = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    object_type = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    trigger_stage = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    metadata_json = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_definitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_workflow_definitions_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_workflow_definitions_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "domain_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    application_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    workflow_definition_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    integration_inbound_event_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    event_type = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    object_type = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    external_object_id = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    idempotency_key = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    payload_json = table.Column<string>(type: "TEXT", nullable: true),
                    object_context_json = table.Column<string>(type: "TEXT", nullable: true),
                    status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    error = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_domain_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_domain_events_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_domain_events_integration_inbound_events_integration_inbound_event_id",
                        column: x => x.integration_inbound_event_id,
                        principalTable: "integration_inbound_events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_domain_events_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_domain_events_workflow_definitions_workflow_definition_id",
                        column: x => x.workflow_definition_id,
                        principalTable: "workflow_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "workflow_steps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    workflow_definition_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    step_order = table.Column<int>(type: "INTEGER", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    step_type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    instruction = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    retrieval_query = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    required_context_json = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_steps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_workflow_steps_workflow_definitions_workflow_definition_id",
                        column: x => x.workflow_definition_id,
                        principalTable: "workflow_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ai_recommendations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    application_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    domain_event_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    workflow_definition_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    object_type = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    external_object_id = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    summary = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    recommended_next_steps_json = table.Column<string>(type: "TEXT", nullable: false),
                    risks_json = table.Column<string>(type: "TEXT", nullable: false),
                    clarification_questions_json = table.Column<string>(type: "TEXT", nullable: false),
                    suggested_tasks_json = table.Column<string>(type: "TEXT", nullable: false),
                    warnings_json = table.Column<string>(type: "TEXT", nullable: false),
                    won_lost_signals_json = table.Column<string>(type: "TEXT", nullable: false),
                    reasoning_label = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    sources_json = table.Column<string>(type: "TEXT", nullable: false),
                    status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    feedback_value = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    feedback_note = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    feedback_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    feedback_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_recommendations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ai_recommendations_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_recommendations_domain_events_domain_event_id",
                        column: x => x.domain_event_id,
                        principalTable: "domain_events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_recommendations_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_recommendations_users_feedback_by_user_id",
                        column: x => x.feedback_by_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ai_recommendations_workflow_definitions_workflow_definition_id",
                        column: x => x.workflow_definition_id,
                        principalTable: "workflow_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_recommendations_application_id",
                table: "ai_recommendations",
                column: "application_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_recommendations_domain_event_id",
                table: "ai_recommendations",
                column: "domain_event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_recommendations_feedback_by_user_id",
                table: "ai_recommendations",
                column: "feedback_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_recommendations_tenant_id",
                table: "ai_recommendations",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_recommendations_tenant_id_application_id_object_type_external_object_id_created_at",
                table: "ai_recommendations",
                columns: new[] { "tenant_id", "application_id", "object_type", "external_object_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_recommendations_tenant_id_status_created_at",
                table: "ai_recommendations",
                columns: new[] { "tenant_id", "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_recommendations_workflow_definition_id",
                table: "ai_recommendations",
                column: "workflow_definition_id");

            migrationBuilder.CreateIndex(
                name: "IX_domain_events_application_id",
                table: "domain_events",
                column: "application_id");

            migrationBuilder.CreateIndex(
                name: "IX_domain_events_integration_inbound_event_id",
                table: "domain_events",
                column: "integration_inbound_event_id");

            migrationBuilder.CreateIndex(
                name: "IX_domain_events_tenant_id",
                table: "domain_events",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_domain_events_tenant_id_application_id_event_type_occurred_at",
                table: "domain_events",
                columns: new[] { "tenant_id", "application_id", "event_type", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "IX_domain_events_tenant_id_application_id_idempotency_key",
                table: "domain_events",
                columns: new[] { "tenant_id", "application_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_domain_events_tenant_id_application_id_object_type_external_object_id",
                table: "domain_events",
                columns: new[] { "tenant_id", "application_id", "object_type", "external_object_id" });

            migrationBuilder.CreateIndex(
                name: "IX_domain_events_workflow_definition_id",
                table: "domain_events",
                column: "workflow_definition_id");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_definitions_application_id",
                table: "workflow_definitions",
                column: "application_id");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_definitions_tenant_id",
                table: "workflow_definitions",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_definitions_tenant_id_application_id_event_type_object_type_trigger_stage",
                table: "workflow_definitions",
                columns: new[] { "tenant_id", "application_id", "event_type", "object_type", "trigger_stage" });

            migrationBuilder.CreateIndex(
                name: "IX_workflow_definitions_tenant_id_status",
                table: "workflow_definitions",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_workflow_steps_tenant_id",
                table: "workflow_steps",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_steps_tenant_id_workflow_definition_id_step_order",
                table: "workflow_steps",
                columns: new[] { "tenant_id", "workflow_definition_id", "step_order" });

            migrationBuilder.CreateIndex(
                name: "IX_workflow_steps_workflow_definition_id",
                table: "workflow_steps",
                column: "workflow_definition_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_recommendations");

            migrationBuilder.DropTable(
                name: "workflow_steps");

            migrationBuilder.DropTable(
                name: "domain_events");

            migrationBuilder.DropTable(
                name: "workflow_definitions");
        }
    }
}
