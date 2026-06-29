using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BiobankApi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "patient",
                columns: table => new
                {
                    PatientId = table.Column<string>(type: "text", nullable: false),
                    Biobank = table.Column<string>(type: "text", nullable: true),
                    Consent = table.Column<bool>(type: "boolean", nullable: true),
                    Sex = table.Column<string>(type: "text", nullable: true),
                    BirthYear = table.Column<int>(type: "integer", nullable: true),
                    BirthMonth = table.Column<int>(type: "integer", nullable: true),
                    AccessionNumbers = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_patient", x => x.PatientId);
                });

            migrationBuilder.CreateTable(
                name: "diagnostic_specimen",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SampleId = table.Column<string>(type: "text", nullable: false),
                    PatientId = table.Column<string>(type: "text", nullable: false),
                    SpecimenNumber = table.Column<int>(type: "integer", nullable: true),
                    Year = table.Column<int>(type: "integer", nullable: true),
                    MaterialType = table.Column<string>(type: "text", nullable: true),
                    Diagnosis = table.Column<string>(type: "text", nullable: true),
                    TakingDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Retrieved = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_diagnostic_specimen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_diagnostic_specimen_patient_PatientId",
                        column: x => x.PatientId,
                        principalTable: "patient",
                        principalColumn: "PatientId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "genome_sample",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TakingDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Retrieved = table.Column<string>(type: "text", nullable: true),
                    SampleId = table.Column<string>(type: "text", nullable: false),
                    PatientId = table.Column<string>(type: "text", nullable: false),
                    MaterialType = table.Column<string>(type: "text", nullable: false),
                    EventNumber = table.Column<int>(type: "integer", nullable: true),
                    CollectionYear = table.Column<int>(type: "integer", nullable: true),
                    Biopsy = table.Column<string>(type: "text", nullable: true),
                    PredictiveNumber = table.Column<string>(type: "text", nullable: true),
                    SamplesNo = table.Column<int>(type: "integer", nullable: true),
                    AvailableSamplesNo = table.Column<int>(type: "integer", nullable: true),
                    AccessionNumbers = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_genome_sample", x => x.Id);
                    table.ForeignKey(
                        name: "FK_genome_sample_patient_PatientId",
                        column: x => x.PatientId,
                        principalTable: "patient",
                        principalColumn: "PatientId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "serum_sample",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Diagnosis = table.Column<string>(type: "text", nullable: true),
                    TakingDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Retrieved = table.Column<string>(type: "text", nullable: true),
                    SampleId = table.Column<string>(type: "text", nullable: false),
                    PatientId = table.Column<string>(type: "text", nullable: false),
                    MaterialType = table.Column<string>(type: "text", nullable: false),
                    EventNumber = table.Column<int>(type: "integer", nullable: true),
                    CollectionYear = table.Column<int>(type: "integer", nullable: true),
                    Biopsy = table.Column<string>(type: "text", nullable: true),
                    PredictiveNumber = table.Column<string>(type: "text", nullable: true),
                    SamplesNo = table.Column<int>(type: "integer", nullable: true),
                    AvailableSamplesNo = table.Column<int>(type: "integer", nullable: true),
                    AccessionNumbers = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_serum_sample", x => x.Id);
                    table.ForeignKey(
                        name: "FK_serum_sample_patient_PatientId",
                        column: x => x.PatientId,
                        principalTable: "patient",
                        principalColumn: "PatientId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tissue_sample",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Diagnosis = table.Column<string>(type: "text", nullable: true),
                    PTnm = table.Column<string>(type: "text", nullable: true),
                    Morphology = table.Column<string>(type: "text", nullable: true),
                    CutTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    FreezeTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Retrieved = table.Column<string>(type: "text", nullable: true),
                    SampleId = table.Column<string>(type: "text", nullable: false),
                    PatientId = table.Column<string>(type: "text", nullable: false),
                    MaterialType = table.Column<string>(type: "text", nullable: false),
                    EventNumber = table.Column<int>(type: "integer", nullable: true),
                    CollectionYear = table.Column<int>(type: "integer", nullable: true),
                    Biopsy = table.Column<string>(type: "text", nullable: true),
                    PredictiveNumber = table.Column<string>(type: "text", nullable: true),
                    SamplesNo = table.Column<int>(type: "integer", nullable: true),
                    AvailableSamplesNo = table.Column<int>(type: "integer", nullable: true),
                    AccessionNumbers = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tissue_sample", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tissue_sample_patient_PatientId",
                        column: x => x.PatientId,
                        principalTable: "patient",
                        principalColumn: "PatientId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_diagnostic_specimen_PatientId",
                table: "diagnostic_specimen",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_diagnostic_specimen_SampleId",
                table: "diagnostic_specimen",
                column: "SampleId");

            migrationBuilder.CreateIndex(
                name: "IX_genome_sample_PatientId",
                table: "genome_sample",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_genome_sample_SampleId",
                table: "genome_sample",
                column: "SampleId");

            migrationBuilder.CreateIndex(
                name: "IX_serum_sample_PatientId",
                table: "serum_sample",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_serum_sample_SampleId",
                table: "serum_sample",
                column: "SampleId");

            migrationBuilder.CreateIndex(
                name: "IX_tissue_sample_PatientId",
                table: "tissue_sample",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_tissue_sample_SampleId",
                table: "tissue_sample",
                column: "SampleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "diagnostic_specimen");

            migrationBuilder.DropTable(
                name: "genome_sample");

            migrationBuilder.DropTable(
                name: "serum_sample");

            migrationBuilder.DropTable(
                name: "tissue_sample");

            migrationBuilder.DropTable(
                name: "patient");
        }
    }
}
