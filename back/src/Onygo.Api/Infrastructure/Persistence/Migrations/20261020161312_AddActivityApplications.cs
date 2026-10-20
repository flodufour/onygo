using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Onygo.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityApplications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActivityApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicantId = table.Column<string>(type: "text", nullable: false),
                    Message = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AppliedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivityApplications_Activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "Activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActivityApplications_AspNetUsers_ApplicantId",
                        column: x => x.ApplicantId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityApplications_ActivityId_ApplicantId",
                table: "ActivityApplications",
                columns: new[] { "ActivityId", "ApplicantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ActivityApplications_ApplicantId_AppliedAt",
                table: "ActivityApplications",
                columns: new[] { "ApplicantId", "AppliedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityApplications");
        }
    }
}
