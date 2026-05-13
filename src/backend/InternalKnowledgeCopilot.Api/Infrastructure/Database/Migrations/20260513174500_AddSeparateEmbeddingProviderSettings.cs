using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260513174500_AddSeparateEmbeddingProviderSettings")]
    public partial class AddSeparateEmbeddingProviderSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "embedding_api_key",
                table: "ai_provider_settings",
                type: "TEXT",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "embedding_api_key_header_name",
                table: "ai_provider_settings",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "Authorization");

            migrationBuilder.AddColumn<string>(
                name: "embedding_base_url",
                table: "ai_provider_settings",
                type: "TEXT",
                maxLength: 1000,
                nullable: false,
                defaultValue: "https://api.openai.com/v1");

            migrationBuilder.AddColumn<string>(
                name: "embedding_provider_name",
                table: "ai_provider_settings",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "openai-compatible");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "embedding_api_key",
                table: "ai_provider_settings");

            migrationBuilder.DropColumn(
                name: "embedding_api_key_header_name",
                table: "ai_provider_settings");

            migrationBuilder.DropColumn(
                name: "embedding_base_url",
                table: "ai_provider_settings");

            migrationBuilder.DropColumn(
                name: "embedding_provider_name",
                table: "ai_provider_settings");
        }
    }
}
