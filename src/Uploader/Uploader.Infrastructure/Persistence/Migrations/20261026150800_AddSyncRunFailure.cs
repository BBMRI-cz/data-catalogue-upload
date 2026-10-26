using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Uploader.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSyncRunFailure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Failure",
                table: "sync_run",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Failure",
                table: "sync_run");
        }
    }
}
