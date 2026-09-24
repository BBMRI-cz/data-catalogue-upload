using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SequencingApi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSamplePredictiveKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PredictiveKey",
                table: "sample",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_sample_PredictiveKey",
                table: "sample",
                column: "PredictiveKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_sample_PredictiveKey",
                table: "sample");

            migrationBuilder.DropColumn(
                name: "PredictiveKey",
                table: "sample");
        }
    }
}
