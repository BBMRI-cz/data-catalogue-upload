using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SequencingApi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenamePredictiveNumberAndIndexIt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SubjectRef",
                table: "sample",
                newName: "PredictiveNumber");

            migrationBuilder.CreateIndex(
                name: "IX_sample_PredictiveNumber",
                table: "sample",
                column: "PredictiveNumber");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_sample_PredictiveNumber",
                table: "sample");

            migrationBuilder.RenameColumn(
                name: "PredictiveNumber",
                table: "sample",
                newName: "SubjectRef");
        }
    }
}
