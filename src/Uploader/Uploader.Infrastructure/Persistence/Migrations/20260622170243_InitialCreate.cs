using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Uploader.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "patient_sync_state",
                columns: table => new
                {
                    PatientId = table.Column<string>(type: "text", nullable: false),
                    SourceFingerprint = table.Column<string>(type: "text", nullable: false),
                    CatalogueRemoteId = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSyncedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "text", nullable: true),
                    RunId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_patient_sync_state", x => x.PatientId);
                });

            migrationBuilder.CreateTable(
                name: "sync_run",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ScannedCount = table.Column<int>(type: "integer", nullable: false),
                    ChangedCount = table.Column<int>(type: "integer", nullable: false),
                    UploadedCount = table.Column<int>(type: "integer", nullable: false),
                    DeletedCount = table.Column<int>(type: "integer", nullable: false),
                    SkippedCount = table.Column<int>(type: "integer", nullable: false),
                    FailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sync_run", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "imaging_study_sync_state",
                columns: table => new
                {
                    AccessionNumber = table.Column<string>(type: "text", nullable: false),
                    PatientId = table.Column<string>(type: "text", nullable: false),
                    SourceFingerprint = table.Column<string>(type: "text", nullable: false),
                    CatalogueRemoteId = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSyncedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "text", nullable: true),
                    RunId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_imaging_study_sync_state", x => x.AccessionNumber);
                    table.ForeignKey(
                        name: "FK_imaging_study_sync_state_patient_sync_state_PatientId",
                        column: x => x.PatientId,
                        principalTable: "patient_sync_state",
                        principalColumn: "PatientId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sample_sync_state",
                columns: table => new
                {
                    SampleId = table.Column<string>(type: "text", nullable: false),
                    PatientId = table.Column<string>(type: "text", nullable: false),
                    SourceFingerprint = table.Column<string>(type: "text", nullable: false),
                    CatalogueRemoteId = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSyncedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "text", nullable: true),
                    RunId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sample_sync_state", x => x.SampleId);
                    table.ForeignKey(
                        name: "FK_sample_sync_state_patient_sync_state_PatientId",
                        column: x => x.PatientId,
                        principalTable: "patient_sync_state",
                        principalColumn: "PatientId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sequencing_sync_state",
                columns: table => new
                {
                    PredictiveNumber = table.Column<string>(type: "text", nullable: false),
                    SampleId = table.Column<string>(type: "text", nullable: false),
                    SourceFingerprint = table.Column<string>(type: "text", nullable: false),
                    CatalogueRemoteId = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSyncedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "text", nullable: true),
                    RunId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sequencing_sync_state", x => x.PredictiveNumber);
                    table.ForeignKey(
                        name: "FK_sequencing_sync_state_sample_sync_state_SampleId",
                        column: x => x.SampleId,
                        principalTable: "sample_sync_state",
                        principalColumn: "SampleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "wsi_sync_state",
                columns: table => new
                {
                    BiopticNumber = table.Column<string>(type: "text", nullable: false),
                    SampleId = table.Column<string>(type: "text", nullable: false),
                    SourceFingerprint = table.Column<string>(type: "text", nullable: false),
                    CatalogueRemoteId = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSyncedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "text", nullable: true),
                    RunId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wsi_sync_state", x => x.BiopticNumber);
                    table.ForeignKey(
                        name: "FK_wsi_sync_state_sample_sync_state_SampleId",
                        column: x => x.SampleId,
                        principalTable: "sample_sync_state",
                        principalColumn: "SampleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_imaging_study_sync_state_PatientId",
                table: "imaging_study_sync_state",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_imaging_study_sync_state_RunId",
                table: "imaging_study_sync_state",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_imaging_study_sync_state_Status",
                table: "imaging_study_sync_state",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_patient_sync_state_RunId",
                table: "patient_sync_state",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_patient_sync_state_Status",
                table: "patient_sync_state",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_sample_sync_state_PatientId",
                table: "sample_sync_state",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_sample_sync_state_RunId",
                table: "sample_sync_state",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_sample_sync_state_Status",
                table: "sample_sync_state",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_sequencing_sync_state_RunId",
                table: "sequencing_sync_state",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_sequencing_sync_state_SampleId",
                table: "sequencing_sync_state",
                column: "SampleId");

            migrationBuilder.CreateIndex(
                name: "IX_sequencing_sync_state_Status",
                table: "sequencing_sync_state",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_wsi_sync_state_RunId",
                table: "wsi_sync_state",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_wsi_sync_state_SampleId",
                table: "wsi_sync_state",
                column: "SampleId");

            migrationBuilder.CreateIndex(
                name: "IX_wsi_sync_state_Status",
                table: "wsi_sync_state",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "imaging_study_sync_state");

            migrationBuilder.DropTable(
                name: "sequencing_sync_state");

            migrationBuilder.DropTable(
                name: "sync_run");

            migrationBuilder.DropTable(
                name: "wsi_sync_state");

            migrationBuilder.DropTable(
                name: "sample_sync_state");

            migrationBuilder.DropTable(
                name: "patient_sync_state");
        }
    }
}
