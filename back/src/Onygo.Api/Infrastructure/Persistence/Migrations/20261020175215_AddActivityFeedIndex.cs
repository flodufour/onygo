using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Onygo.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityFeedIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Activities_Status_StartsAt",
                table: "Activities",
                columns: new[] { "Status", "StartsAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Activities_Status_StartsAt",
                table: "Activities");
        }
    }
}
