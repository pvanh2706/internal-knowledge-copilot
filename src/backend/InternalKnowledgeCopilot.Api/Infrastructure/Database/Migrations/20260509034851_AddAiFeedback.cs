using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddAiFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_feedback",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ai_interaction_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    value = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    note = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    review_status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    reviewed_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    reviewer_note = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    resolved_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_feedback", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ai_feedback_ai_interactions_ai_interaction_id",
                        column: x => x.ai_interaction_id,
                        principalTable: "ai_interactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ai_feedback_users_reviewed_by_user_id",
                        column: x => x.reviewed_by_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_feedback_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_feedback_ai_interaction_id_user_id",
                table: "ai_feedback",
                columns: new[] { "ai_interaction_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_feedback_reviewed_by_user_id",
                table: "ai_feedback",
                column: "reviewed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_feedback_user_id",
                table: "ai_feedback",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_feedback_value_review_status",
                table: "ai_feedback",
                columns: new[] { "value", "review_status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_feedback");
        }
    }
}
