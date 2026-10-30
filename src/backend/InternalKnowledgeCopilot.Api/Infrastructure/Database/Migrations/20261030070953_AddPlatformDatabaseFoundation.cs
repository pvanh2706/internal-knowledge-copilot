using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformDatabaseFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "platform_admins",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    email_normalized = table.Column<string>(type: "TEXT", maxLength: 320, nullable: false),
                    display_name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    password_hash = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    failed_login_count = table.Column<int>(type: "INTEGER", nullable: false),
                    locked_until = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    must_change_password = table.Column<bool>(type: "INTEGER", nullable: false),
                    token_version = table.Column<int>(type: "INTEGER", nullable: false),
                    last_login_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_admins", x => x.id);
                    table.CheckConstraint("CK_platform_admins_display_name", "length(trim(\"display_name\")) > 0");
                    table.CheckConstraint("CK_platform_admins_email_normalized", "\"email_normalized\" = lower(trim(\"email_normalized\")) AND instr(\"email_normalized\", '@') > 1");
                    table.CheckConstraint("CK_platform_admins_failed_login_count", "\"failed_login_count\" >= 0");
                    table.CheckConstraint("CK_platform_admins_password_hash_versioned", "\"password_hash\" GLOB '$[a-z]*$v=[1-9]*$*$*$*'");
                    table.CheckConstraint("CK_platform_admins_status", "\"status\" IN ('Active', 'Disabled', 'Locked')");
                    table.CheckConstraint("CK_platform_admins_token_version", "\"token_version\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "platform_audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    actor_platform_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    action = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    target_tenant_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    target_type = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    target_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    outcome = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    request_id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    metadata_json = table.Column<string>(type: "TEXT", nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_audit_logs", x => x.id);
                    table.CheckConstraint("CK_platform_audit_logs_action", "length(trim(\"action\")) > 0");
                    table.CheckConstraint("CK_platform_audit_logs_metadata_json", "\"metadata_json\" IS NULL OR json_valid(\"metadata_json\")");
                    table.CheckConstraint("CK_platform_audit_logs_outcome", "\"outcome\" IN ('Success', 'Denied', 'Failed')");
                    table.ForeignKey(
                        name: "FK_platform_audit_logs_platform_admins_actor_platform_id",
                        column: x => x.actor_platform_id,
                        principalTable: "platform_admins",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_platform_audit_logs_tenants_target_tenant_id",
                        column: x => x.target_tenant_id,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "platform_security_state",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false),
                    deployment_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    bootstrap_status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    bootstrap_admin_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    bootstrap_completed_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    state_version = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_security_state", x => x.id);
                    table.CheckConstraint("CK_platform_security_state_bootstrap", "(\"bootstrap_status\" = 'NotStarted' AND \"bootstrap_admin_id\" IS NULL AND \"bootstrap_completed_at\" IS NULL) OR (\"bootstrap_status\" = 'Completed' AND \"bootstrap_admin_id\" IS NOT NULL AND \"bootstrap_completed_at\" IS NOT NULL)");
                    table.CheckConstraint("CK_platform_security_state_singleton", "\"id\" = 1");
                    table.CheckConstraint("CK_platform_security_state_state_version", "\"state_version\" >= 0");
                    table.ForeignKey(
                        name: "FK_platform_security_state_platform_admins_bootstrap_admin_id",
                        column: x => x.bootstrap_admin_id,
                        principalTable: "platform_admins",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_platform_admins_email_normalized",
                table: "platform_admins",
                column: "email_normalized",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_platform_admins_status",
                table: "platform_admins",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_platform_audit_logs_action_occurred_at",
                table: "platform_audit_logs",
                columns: new[] { "action", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "IX_platform_audit_logs_actor_platform_id_occurred_at",
                table: "platform_audit_logs",
                columns: new[] { "actor_platform_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "IX_platform_audit_logs_occurred_at",
                table: "platform_audit_logs",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "IX_platform_audit_logs_target_tenant_id_occurred_at",
                table: "platform_audit_logs",
                columns: new[] { "target_tenant_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "IX_platform_security_state_bootstrap_admin_id",
                table: "platform_security_state",
                column: "bootstrap_admin_id");

            // Tài khoản platform không bị xóa cứng (giữ lịch sử audit); vô hiệu hóa bằng status.
            migrationBuilder.Sql("""
                CREATE TRIGGER "trg_platform_admins_no_delete"
                BEFORE DELETE ON "platform_admins"
                BEGIN
                    SELECT RAISE(ABORT, 'platform_admins_delete_forbidden');
                END;
                """);

            // Audit platform là append-only.
            migrationBuilder.Sql("""
                CREATE TRIGGER "trg_platform_audit_logs_no_update"
                BEFORE UPDATE ON "platform_audit_logs"
                BEGIN
                    SELECT RAISE(ABORT, 'platform_audit_logs_append_only');
                END;
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER "trg_platform_audit_logs_no_delete"
                BEFORE DELETE ON "platform_audit_logs"
                BEGIN
                    SELECT RAISE(ABORT, 'platform_audit_logs_append_only');
                END;
                """);

            // Xóa bản ghi trạng thái sẽ mở lại bootstrap ngầm, nên bị cấm ở tầng DB.
            migrationBuilder.Sql("""
                CREATE TRIGGER "trg_platform_security_state_no_delete"
                BEFORE DELETE ON "platform_security_state"
                BEGIN
                    SELECT RAISE(ABORT, 'platform_security_state_delete_forbidden');
                END;
                """);

            // Danh tính deployment bất biến; Completed không thể đảo ngược hay đổi admin/thời điểm;
            // state_version phải tăng đúng 1 (khóa lạc quan do ứng dụng quản lý).
            migrationBuilder.Sql("""
                CREATE TRIGGER "trg_platform_security_state_guard_update"
                BEFORE UPDATE ON "platform_security_state"
                BEGIN
                    SELECT RAISE(ABORT, 'platform_security_state_identity_immutable')
                    WHERE NEW."id" IS NOT OLD."id"
                        OR NEW."deployment_id" IS NOT OLD."deployment_id"
                        OR NEW."created_at" IS NOT OLD."created_at";
                    SELECT RAISE(ABORT, 'platform_bootstrap_completed_immutable')
                    WHERE OLD."bootstrap_status" = 'Completed'
                        AND (NEW."bootstrap_status" IS NOT OLD."bootstrap_status"
                            OR NEW."bootstrap_admin_id" IS NOT OLD."bootstrap_admin_id"
                            OR NEW."bootstrap_completed_at" IS NOT OLD."bootstrap_completed_at");
                    SELECT RAISE(ABORT, 'platform_security_state_version_conflict')
                    WHERE NEW."state_version" IS NOT OLD."state_version" + 1;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rollback không được âm thầm xóa lịch sử bảo mật/audit: chỉ cho phép khi cả ba bảng platform còn rỗng.
            // Nếu có dữ liệu, CHECK của bảng tạm thất bại và migration dừng trước khi drop bất kỳ bảng nào.
            migrationBuilder.Sql("""
                CREATE TEMP TABLE "__platform_rollback_guard" (
                    "platform_rows" INTEGER NOT NULL
                        CONSTRAINT "platform_rollback_refused_platform_tables_not_empty" CHECK ("platform_rows" = 0)
                );
                """);
            migrationBuilder.Sql("""
                INSERT INTO temp."__platform_rollback_guard" ("platform_rows")
                SELECT (SELECT COUNT(*) FROM "platform_admins")
                    + (SELECT COUNT(*) FROM "platform_audit_logs")
                    + (SELECT COUNT(*) FROM "platform_security_state");
                """);
            migrationBuilder.Sql("""DROP TABLE temp."__platform_rollback_guard";""");

            migrationBuilder.DropTable(
                name: "platform_audit_logs");

            migrationBuilder.DropTable(
                name: "platform_security_state");

            migrationBuilder.DropTable(
                name: "platform_admins");
        }
    }
}
