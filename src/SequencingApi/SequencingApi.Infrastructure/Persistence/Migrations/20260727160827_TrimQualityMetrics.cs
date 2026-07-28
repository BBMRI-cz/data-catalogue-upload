using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SequencingApi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TrimQualityMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AlignedReads",
                table: "quality_metrics");

            migrationBuilder.DropColumn(
                name: "AverageCoverage",
                table: "quality_metrics");

            migrationBuilder.DropColumn(
                name: "HeterozygousVariants",
                table: "quality_metrics");

            migrationBuilder.DropColumn(
                name: "HomozygousVariants",
                table: "quality_metrics");

            migrationBuilder.DropColumn(
                name: "OnTargetRatePercent",
                table: "quality_metrics");

            migrationBuilder.DropColumn(
                name: "PctTargetOver100x",
                table: "quality_metrics");

            migrationBuilder.DropColumn(
                name: "TotalReads",
                table: "quality_metrics");

            migrationBuilder.DropColumn(
                name: "TotalVariants",
                table: "quality_metrics");

            migrationBuilder.DropColumn(
                name: "TsTvRatio",
                table: "quality_metrics");

            migrationBuilder.DropColumn(
                name: "Verdict",
                table: "quality_metrics");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "AlignedReads",
                table: "quality_metrics",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AverageCoverage",
                table: "quality_metrics",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HeterozygousVariants",
                table: "quality_metrics",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HomozygousVariants",
                table: "quality_metrics",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "OnTargetRatePercent",
                table: "quality_metrics",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PctTargetOver100x",
                table: "quality_metrics",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "TotalReads",
                table: "quality_metrics",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TotalVariants",
                table: "quality_metrics",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "TsTvRatio",
                table: "quality_metrics",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Verdict",
                table: "quality_metrics",
                type: "text",
                nullable: true);
        }
    }
}
