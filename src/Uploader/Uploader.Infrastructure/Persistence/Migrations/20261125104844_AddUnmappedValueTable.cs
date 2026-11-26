using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Uploader.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUnmappedValueTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "unmapped_value",
                columns: table => new
                {
                    Field = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    Context = table.Column<string>(type: "text", nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false),
                    FirstSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastRunId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_unmapped_value", x => new { x.Field, x.Value, x.Context });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "unmapped_value");
        }
    }
}
