using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddFoldersAndPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "folders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    parent_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    path = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_folders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_folders_folders_parent_id",
                        column: x => x.parent_id,
                        principalTable: "folders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_folders_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "folder_permissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    folder_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    team_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    can_view = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_folder_permissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_folder_permissions_folders_folder_id",
                        column: x => x.folder_id,
                        principalTable: "folders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_folder_permissions_teams_team_id",
                        column: x => x.team_id,
                        principalTable: "teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_folder_permissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    folder_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    can_view = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_folder_permissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_folder_permissions_folders_folder_id",
                        column: x => x.folder_id,
                        principalTable: "folders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_folder_permissions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_folder_permissions_folder_id_team_id",
                table: "folder_permissions",
                columns: new[] { "folder_id", "team_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_folder_permissions_team_id",
                table: "folder_permissions",
                column: "team_id");

            migrationBuilder.CreateIndex(
                name: "IX_folders_created_by_user_id",
                table: "folders",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_folders_parent_id",
                table: "folders",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "IX_folders_path",
                table: "folders",
                column: "path",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_folder_permissions_folder_id",
                table: "user_folder_permissions",
                column: "folder_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_folder_permissions_user_id_folder_id",
                table: "user_folder_permissions",
                columns: new[] { "user_id", "folder_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "folder_permissions");

            migrationBuilder.DropTable(
                name: "user_folder_permissions");

            migrationBuilder.DropTable(
                name: "folders");
        }
    }
}
