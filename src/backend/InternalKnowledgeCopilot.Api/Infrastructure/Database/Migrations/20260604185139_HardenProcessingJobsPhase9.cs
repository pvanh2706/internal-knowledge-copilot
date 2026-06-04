using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class HardenProcessingJobsPhase9 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_processing_jobs_tenant_id_status_created_at",
                table: "processing_jobs");

            migrationBuilder.AddColumn<Guid>(
                name: "application_id",
                table: "processing_jobs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "dead_lettered_at",
                table: "processing_jobs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "error_code",
                table: "processing_jobs",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "error_details_json",
                table: "processing_jobs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "error_type",
                table: "processing_jobs",
                type: "TEXT",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "idempotency_key",
                table: "processing_jobs",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_attempt_at",
                table: "processing_jobs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_error_at",
                table: "processing_jobs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "scheduled_at",
                table: "processing_jobs",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.CreateIndex(
                name: "IX_processing_jobs_application_id",
                table: "processing_jobs",
                column: "application_id");

            migrationBuilder.CreateIndex(
                name: "IX_processing_jobs_tenant_id_application_id_job_type_idempotency_key",
                table: "processing_jobs",
                columns: new[] { "tenant_id", "application_id", "job_type", "idempotency_key" },
                unique: true,
                filter: "idempotency_key IS NOT NULL AND application_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_processing_jobs_tenant_id_application_id_status_scheduled_at",
                table: "processing_jobs",
                columns: new[] { "tenant_id", "application_id", "status", "scheduled_at" });

            migrationBuilder.AddForeignKey(
                name: "FK_processing_jobs_applications_application_id",
                table: "processing_jobs",
                column: "application_id",
                principalTable: "applications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_processing_jobs_tenants_tenant_id",
                table: "processing_jobs",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_processing_jobs_applications_application_id",
                table: "processing_jobs");

            migrationBuilder.DropForeignKey(
                name: "FK_processing_jobs_tenants_tenant_id",
                table: "processing_jobs");

            migrationBuilder.DropIndex(
                name: "IX_processing_jobs_application_id",
                table: "processing_jobs");

            migrationBuilder.DropIndex(
                name: "IX_processing_jobs_tenant_id_application_id_job_type_idempotency_key",
                table: "processing_jobs");

            migrationBuilder.DropIndex(
                name: "IX_processing_jobs_tenant_id_application_id_status_scheduled_at",
                table: "processing_jobs");

            migrationBuilder.DropColumn(
                name: "application_id",
                table: "processing_jobs");

            migrationBuilder.DropColumn(
                name: "dead_lettered_at",
                table: "processing_jobs");

            migrationBuilder.DropColumn(
                name: "error_code",
                table: "processing_jobs");

            migrationBuilder.DropColumn(
                name: "error_details_json",
                table: "processing_jobs");

            migrationBuilder.DropColumn(
                name: "error_type",
                table: "processing_jobs");

            migrationBuilder.DropColumn(
                name: "idempotency_key",
                table: "processing_jobs");

            migrationBuilder.DropColumn(
                name: "last_attempt_at",
                table: "processing_jobs");

            migrationBuilder.DropColumn(
                name: "last_error_at",
                table: "processing_jobs");

            migrationBuilder.DropColumn(
                name: "scheduled_at",
                table: "processing_jobs");

            migrationBuilder.CreateIndex(
                name: "IX_processing_jobs_tenant_id_status_created_at",
                table: "processing_jobs",
                columns: new[] { "tenant_id", "status", "created_at" });
        }
    }
}
