using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStatementIngestion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "statements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    household_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    uploader_user_id = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    original_filename = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    format = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    uploaded_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    storage_key = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    parsed_transaction_count = table.Column<int>(type: "int", nullable: true),
                    error_message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    parse_summary_json = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_statements", x => x.id);
                    table.ForeignKey(
                        name: "FK_statements_households_household_id",
                        column: x => x.household_id,
                        principalTable: "households",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_statements_household_uploaded_at",
                table: "statements",
                columns: new[] { "household_id", "uploaded_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "statements");
        }
    }
}
