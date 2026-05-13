using System;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260513170000_AddAiProviderSettings")]
    public partial class AddAiProviderSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_provider_settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    base_url = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    api_key = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    api_key_header_name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    chat_endpoint_mode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    chat_model = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    fast_model = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    embedding_model = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    embedding_dimension = table.Column<int>(type: "INTEGER", nullable: false),
                    reasoning_effort = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    temperature = table.Column<double>(type: "REAL", nullable: true),
                    max_output_tokens = table.Column<int>(type: "INTEGER", nullable: false),
                    timeout_seconds = table.Column<int>(type: "INTEGER", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_provider_settings", x => x.id);
                    table.ForeignKey(
                        name: "FK_ai_provider_settings_users_updated_by_user_id",
                        column: x => x.updated_by_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_provider_settings_updated_by_user_id",
                table: "ai_provider_settings",
                column: "updated_by_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_provider_settings");
        }
    }
}
