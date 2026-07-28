using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SequencingApi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRunClusterStatistics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ClusterCountPassingFilter",
                table: "sequencing_run",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ClusterDensity",
                table: "sequencing_run",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompletionStatus",
                table: "sequencing_run",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ErrorDescription",
                table: "sequencing_run",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "EstimatedYield",
                table: "sequencing_run",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PercentageClustersPassingFilter",
                table: "sequencing_run",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClusterCountPassingFilter",
                table: "sequencing_run");

            migrationBuilder.DropColumn(
                name: "ClusterDensity",
                table: "sequencing_run");

            migrationBuilder.DropColumn(
                name: "CompletionStatus",
                table: "sequencing_run");

            migrationBuilder.DropColumn(
                name: "ErrorDescription",
                table: "sequencing_run");

            migrationBuilder.DropColumn(
                name: "EstimatedYield",
                table: "sequencing_run");

            migrationBuilder.DropColumn(
                name: "PercentageClustersPassingFilter",
                table: "sequencing_run");
        }
    }
}
