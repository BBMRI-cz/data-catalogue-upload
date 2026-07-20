using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SequencingApi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "panel",
                columns: table => new
                {
                    PanelId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Abbreviation = table.Column<string>(type: "text", nullable: true),
                    Vendor = table.Column<string>(type: "text", nullable: true),
                    Assay = table.Column<string>(type: "text", nullable: true),
                    CatalogueCode = table.Column<string>(type: "text", nullable: true),
                    Genes = table.Column<string>(type: "text", nullable: false),
                    TargetRegionsRef = table.Column<string>(type: "text", nullable: true),
                    AvailableFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    AvailableTo = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_panel", x => x.PanelId);
                });

            migrationBuilder.CreateTable(
                name: "sample",
                columns: table => new
                {
                    ExternalId = table.Column<string>(type: "text", nullable: false),
                    IdScheme = table.Column<string>(type: "text", nullable: false),
                    SubjectRef = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sample", x => x.ExternalId);
                });

            migrationBuilder.CreateTable(
                name: "sequencing_run",
                columns: table => new
                {
                    RunId = table.Column<string>(type: "text", nullable: false),
                    RunNumber = table.Column<int>(type: "integer", nullable: true),
                    InstrumentModel = table.Column<string>(type: "text", nullable: true),
                    InstrumentId = table.Column<string>(type: "text", nullable: true),
                    Platform = table.Column<string>(type: "text", nullable: true),
                    SourceClass = table.Column<string>(type: "text", nullable: true),
                    RunDate = table.Column<DateOnly>(type: "date", nullable: true),
                    FlowcellId = table.Column<string>(type: "text", nullable: true),
                    LaneCount = table.Column<int>(type: "integer", nullable: true),
                    Assay = table.Column<string>(type: "text", nullable: true),
                    Workflow = table.Column<string>(type: "text", nullable: true),
                    ExperimentName = table.Column<string>(type: "text", nullable: true),
                    Chemistry = table.Column<string>(type: "text", nullable: true),
                    ReagentKit = table.Column<string>(type: "text", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    PercentageQ30 = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sequencing_run", x => x.RunId);
                });

            migrationBuilder.CreateTable(
                name: "run_sample",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SampleExternalId = table.Column<string>(type: "text", nullable: false),
                    RunId = table.Column<string>(type: "text", nullable: false),
                    SampleIndex = table.Column<int>(type: "integer", nullable: true),
                    SampleType = table.Column<string>(type: "text", nullable: true),
                    LaneCount = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_run_sample", x => x.Id);
                    table.ForeignKey(
                        name: "FK_run_sample_sample_SampleExternalId",
                        column: x => x.SampleExternalId,
                        principalTable: "sample",
                        principalColumn: "ExternalId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "run_read",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RunId = table.Column<string>(type: "text", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    NumCycles = table.Column<int>(type: "integer", nullable: false),
                    IsIndexedRead = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_run_read", x => x.Id);
                    table.ForeignKey(
                        name: "FK_run_read_sequencing_run_RunId",
                        column: x => x.RunId,
                        principalTable: "sequencing_run",
                        principalColumn: "RunId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "analysis",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RunSampleId = table.Column<long>(type: "bigint", nullable: false),
                    AnalysisType = table.Column<string>(type: "text", nullable: false),
                    PipelineName = table.Column<string>(type: "text", nullable: false),
                    PipelineVersion = table.Column<string>(type: "text", nullable: true),
                    ReferenceGenome = table.Column<string>(type: "text", nullable: true),
                    ProducedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analysis", x => x.Id);
                    table.ForeignKey(
                        name: "FK_analysis_run_sample_RunSampleId",
                        column: x => x.RunSampleId,
                        principalTable: "run_sample",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "library_preparation",
                columns: table => new
                {
                    RunSampleId = table.Column<long>(type: "bigint", nullable: false),
                    PanelId = table.Column<string>(type: "text", nullable: true),
                    InputAmount = table.Column<int>(type: "integer", nullable: true),
                    LibraryPrepKit = table.Column<string>(type: "text", nullable: true),
                    PcrFree = table.Column<bool>(type: "boolean", nullable: true),
                    TargetEnrichmentKit = table.Column<string>(type: "text", nullable: true),
                    UmiPresent = table.Column<bool>(type: "boolean", nullable: true),
                    IntendedInsertSize = table.Column<int>(type: "integer", nullable: true),
                    IntendedReadLength = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_library_preparation", x => x.RunSampleId);
                    table.ForeignKey(
                        name: "FK_library_preparation_run_sample_RunSampleId",
                        column: x => x.RunSampleId,
                        principalTable: "run_sample",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "quality_metrics",
                columns: table => new
                {
                    AnalysisId = table.Column<long>(type: "bigint", nullable: false),
                    AverageCoverage = table.Column<double>(type: "double precision", nullable: true),
                    PctTargetOver100x = table.Column<double>(type: "double precision", nullable: true),
                    MedianReadDepth = table.Column<int>(type: "integer", nullable: true),
                    ObservedReadLength = table.Column<int>(type: "integer", nullable: true),
                    TotalReads = table.Column<long>(type: "bigint", nullable: true),
                    AlignedReads = table.Column<long>(type: "bigint", nullable: true),
                    OnTargetRatePercent = table.Column<double>(type: "double precision", nullable: true),
                    TotalVariants = table.Column<int>(type: "integer", nullable: true),
                    TsTvRatio = table.Column<double>(type: "double precision", nullable: true),
                    HomozygousVariants = table.Column<int>(type: "integer", nullable: true),
                    HeterozygousVariants = table.Column<int>(type: "integer", nullable: true),
                    Verdict = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quality_metrics", x => x.AnalysisId);
                    table.ForeignKey(
                        name: "FK_quality_metrics_analysis_AnalysisId",
                        column: x => x.AnalysisId,
                        principalTable: "analysis",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sequencing_file",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RunSampleId = table.Column<long>(type: "bigint", nullable: true),
                    AnalysisId = table.Column<long>(type: "bigint", nullable: true),
                    Role = table.Column<string>(type: "text", nullable: false),
                    Path = table.Column<string>(type: "text", nullable: false),
                    Format = table.Column<string>(type: "text", nullable: true),
                    Lane = table.Column<int>(type: "integer", nullable: true),
                    Read = table.Column<int>(type: "integer", nullable: true),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    Checksum = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sequencing_file", x => x.Id);
                    table.CheckConstraint("CK_sequencing_file_single_owner", "(\"RunSampleId\" IS NULL) <> (\"AnalysisId\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_sequencing_file_analysis_AnalysisId",
                        column: x => x.AnalysisId,
                        principalTable: "analysis",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_sequencing_file_run_sample_RunSampleId",
                        column: x => x.RunSampleId,
                        principalTable: "run_sample",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_analysis_RunSampleId",
                table: "analysis",
                column: "RunSampleId");

            migrationBuilder.CreateIndex(
                name: "IX_library_preparation_PanelId",
                table: "library_preparation",
                column: "PanelId");

            migrationBuilder.CreateIndex(
                name: "IX_run_read_RunId_Position",
                table: "run_read",
                columns: new[] { "RunId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_run_sample_RunId",
                table: "run_sample",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_run_sample_SampleExternalId_RunId",
                table: "run_sample",
                columns: new[] { "SampleExternalId", "RunId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sample_IdScheme",
                table: "sample",
                column: "IdScheme");

            migrationBuilder.CreateIndex(
                name: "IX_sequencing_file_AnalysisId",
                table: "sequencing_file",
                column: "AnalysisId");

            migrationBuilder.CreateIndex(
                name: "IX_sequencing_file_Role",
                table: "sequencing_file",
                column: "Role");

            migrationBuilder.CreateIndex(
                name: "IX_sequencing_file_RunSampleId",
                table: "sequencing_file",
                column: "RunSampleId");

            migrationBuilder.CreateIndex(
                name: "IX_sequencing_run_RunDate",
                table: "sequencing_run",
                column: "RunDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "library_preparation");

            migrationBuilder.DropTable(
                name: "panel");

            migrationBuilder.DropTable(
                name: "quality_metrics");

            migrationBuilder.DropTable(
                name: "run_read");

            migrationBuilder.DropTable(
                name: "sequencing_file");

            migrationBuilder.DropTable(
                name: "sequencing_run");

            migrationBuilder.DropTable(
                name: "analysis");

            migrationBuilder.DropTable(
                name: "run_sample");

            migrationBuilder.DropTable(
                name: "sample");
        }
    }
}
