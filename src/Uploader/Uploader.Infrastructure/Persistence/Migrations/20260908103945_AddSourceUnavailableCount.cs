using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Uploader.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSourceUnavailableCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SourceUnavailableCount",
                table: "sync_run",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceUnavailableCount",
                table: "sync_run");
        }
    }
}
