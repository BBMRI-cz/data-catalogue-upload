using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SequencingApi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropAnalysisPipelineVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PipelineVersion",
                table: "analysis");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PipelineVersion",
                table: "analysis",
                type: "text",
                nullable: true);
        }
    }
}
