using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddActionApprovals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_action_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tenant_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    application_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    recommendation_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    action_type = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    target_object_type = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    target_external_object_id = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    payload_json = table.Column<string>(type: "TEXT", nullable: false),
                    normalized_payload_json = table.Column<string>(type: "TEXT", nullable: true),
                    approval_mode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    idempotency_key = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    requested_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    approved_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    rejected_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    executed_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    rejection_reason = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    cancellation_reason = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    validation_result_json = table.Column<string>(type: "TEXT", nullable: true),
                    rule_decision_json = table.Column<string>(type: "TEXT", nullable: true),
                    external_execution_id = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    execution_result_json = table.Column<string>(type: "TEXT", nullable: true),
                    execution_error = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    approved_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    rejected_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    executing_started_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    executed_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_action_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ai_action_requests_ai_recommendations_recommendation_id",
                        column: x => x.recommendation_id,
                        principalTable: "ai_recommendations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_action_requests_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_action_requests_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_action_requests_users_approved_by_user_id",
                        column: x => x.approved_by_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ai_action_requests_users_executed_by_user_id",
                        column: x => x.executed_by_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ai_action_requests_users_rejected_by_user_id",
                        column: x => x.rejected_by_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ai_action_requests_users_requested_by_user_id",
                        column: x => x.requested_by_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_action_requests_application_id",
                table: "ai_action_requests",
                column: "application_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_action_requests_approved_by_user_id",
                table: "ai_action_requests",
                column: "approved_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_action_requests_executed_by_user_id",
                table: "ai_action_requests",
                column: "executed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_action_requests_recommendation_id",
                table: "ai_action_requests",
                column: "recommendation_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_action_requests_rejected_by_user_id",
                table: "ai_action_requests",
                column: "rejected_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_action_requests_requested_by_user_id",
                table: "ai_action_requests",
                column: "requested_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_action_requests_tenant_id",
                table: "ai_action_requests",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_action_requests_tenant_id_application_id_idempotency_key",
                table: "ai_action_requests",
                columns: new[] { "tenant_id", "application_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_action_requests_tenant_id_application_id_status_created_at",
                table: "ai_action_requests",
                columns: new[] { "tenant_id", "application_id", "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_action_requests_tenant_id_application_id_target_object_type_target_external_object_id_created_at",
                table: "ai_action_requests",
                columns: new[] { "tenant_id", "application_id", "target_object_type", "target_external_object_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_action_requests");
        }
    }
}
