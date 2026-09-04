using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Uploader.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPseudonymTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pseudonym",
                columns: table => new
                {
                    Kind = table.Column<string>(type: "text", nullable: false),
                    RealId = table.Column<string>(type: "text", nullable: false),
                    Pseudonym = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pseudonym", x => new { x.Kind, x.RealId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_pseudonym_Kind_Pseudonym",
                table: "pseudonym",
                columns: new[] { "Kind", "Pseudonym" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pseudonym");
        }
    }
}
